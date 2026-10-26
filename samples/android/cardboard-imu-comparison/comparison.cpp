#include <jni.h>
#include <android/asset_manager_jni.h>
#include <android/log.h>
#include <EGL/egl.h>
#include <EGL/eglext.h>
#include <GLES3/gl3.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <deque>
#include <stdexcept>
#include <string>
#include <vector>
#include <time.h>
#include <dlfcn.h>
#include <android/trace.h>
#include "cardboard.h"
#include "comparison_config.h"
#include "pose_timing.h"
#include "pose_diagnostic.h"

#define INFO(...) __android_log_print(ANDROID_LOG_INFO,"SpatialNative",__VA_ARGS__)
#define ERROR(...) __android_log_print(ANDROID_LOG_ERROR,"SpatialNative",__VA_ARGS__)
extern "C" int CardboardUnity_configureHostTracking(float,float,float,float,float,float,float,float,int,int);
extern "C" void CardboardUnity_setHostViewCompensation(int,int);
extern "C" void CardboardUnity_applyHostTracking(float*,float*);
extern float gXimmerseAR_WarpMesh_Verts_L[],gXimmerseAR_WarpMesh_UVs_L[];
extern float gXimmerseAR_WarpMesh_Verts_R[],gXimmerseAR_WarpMesh_UVs_R[];
extern unsigned int gXimmerseAR_WarpMesh_Tris_L[],gXimmerseAR_WarpMesh_Tris_R[];
extern int gXimmerseAR_WarpMesh_Verts_L_Count,gXimmerseAR_WarpMesh_Tris_L_Count;
extern int gXimmerseAR_WarpMesh_Verts_R_Count,gXimmerseAR_WarpMesh_Tris_R_Count;
static int64_t now(clockid_t clock=CLOCK_MONOTONIC) { timespec t{};clock_gettime(clock,&t);return t.tv_sec*1000000000LL+t.tv_nsec; }
using Matrix=std::array<float,16>;
static Matrix identity() { Matrix m{};m[0]=m[5]=m[10]=m[15]=1;return m; }
static Matrix multiply(const Matrix&a,const Matrix&b) {
    Matrix m{}; for(int c=0;c<4;c++)for(int row=0;row<4;row++)for(int k=0;k<4;k++)m[c*4+row]+=a[k*4+row]*b[c*4+k];return m;
}
static Matrix rotation(const std::array<float,4>&q) {
    float x=q[0],y=q[1],z=q[2],w=q[3];Matrix m=identity();
    m[0]=1-2*(y*y+z*z);m[1]=2*(x*y+w*z);m[2]=2*(x*z-w*y);
    m[4]=2*(x*y-w*z);m[5]=1-2*(x*x+z*z);m[6]=2*(y*z+w*x);
    m[8]=2*(x*z+w*y);m[9]=2*(y*z-w*x);m[10]=1-2*(x*x+y*y);return m;
}
static Matrix inverseRotation(const Matrix&r) { auto m=identity();for(int c=0;c<3;c++)for(int row=0;row<3;row++)m[c*4+row]=r[row*4+c];return m; }
struct Warp { std::vector<float> xy,uv;std::vector<int> indices;CardboardMesh mesh{}; };
// Same rectangle mapping, UV direction, triangle clipping and degenerate-strip
// joins as the pinned Spatial Cardboard patch, using its original mesh arrays.
static Warp warp(int eye) {
    const float* p=eye?gXimmerseAR_WarpMesh_Verts_R:gXimmerseAR_WarpMesh_Verts_L;
    const float* uv=eye?gXimmerseAR_WarpMesh_UVs_R:gXimmerseAR_WarpMesh_UVs_L;
    const unsigned int* tris=eye?gXimmerseAR_WarpMesh_Tris_R:gXimmerseAR_WarpMesh_Tris_L;
    int count=(eye?gXimmerseAR_WarpMesh_Verts_R_Count:gXimmerseAR_WarpMesh_Verts_L_Count)/3;
    int ntris=eye?gXimmerseAR_WarpMesh_Tris_R_Count:gXimmerseAR_WarpMesh_Tris_L_Count;
    using V=std::array<float,4>;std::vector<V> vertices;Warp out;
    const float* r=kEyeRects+eye*4;
    for(int i=0;i<count;i++) {
        float u=p[3*i]+(eye?0.f:1.f),v=(1-p[3*i+1])*.5f;
        if(kRotate180) {u=1-u;v=1-v;}
        vertices.push_back({2*(r[0]+u*r[2])/kWidth-1,1-2*(r[1]+v*r[3])/kHeight,(uv[4*i]+1)*.5f,(uv[4*i+1]+1)*.5f});
    }
    const float bounds[]={2*r[0]/kWidth-1,2*(r[0]+r[2])/kWidth-1,1-2*(r[1]+r[3])/kHeight,1-2*r[1]/kHeight};
    for(int t=0;t<ntris;t+=3) {
        std::vector<V> poly{vertices.at(tris[t]),vertices.at(tris[t+1]),vertices.at(tris[t+2])};
        for(int plane=0;plane<4&&!poly.empty();plane++) {
            int axis=plane/2;float sign=plane%2==0?1.f:-1.f;
            auto distance=[&](const V&v){return sign*(v[axis]-bounds[plane]);};
            std::vector<V> next;auto previous=poly.back();float dp=distance(previous);
            for(const auto& current:poly) {
                float dc=distance(current);
                if((dp>=0)!=(dc>=0)) {V v{};float f=dp/(dp-dc);for(int k=0;k<4;k++)v[k]=previous[k]+f*(current[k]-previous[k]);next.push_back(v);}
                if(dc>=0)next.push_back(current);previous=current;dp=dc;
            }
            poly=std::move(next);
        }
        for(size_t k=2;k<poly.size();k++) {
            int base=out.xy.size()/2;
            for(auto v:{poly[0],poly[k-1],poly[k]}) {out.xy.insert(out.xy.end(),{v[0],v[1]});out.uv.insert(out.uv.end(),{v[2],v[3]});}
            if(out.indices.empty())out.indices.insert(out.indices.end(),{base,base+1,base+2});
            else {int last=out.indices.back();out.indices.insert(out.indices.end(),{last,last,base,base,base+1,base+2});}
        }
    }
    return out;
}
struct Vertex { float x,y,z,r,g,b; };
using Vec=std::array<float,3>;
struct Scene {
    std::vector<Vertex> vertices;
    int worldVertexCount=0;
    void tri(Vec a,Vec b,Vec c,Vec color) {for(auto p:{a,b,c})vertices.push_back({p[0],p[1],p[2],color[0],color[1],color[2]});}
    void quad(Vec a,Vec b,Vec c,Vec d,Vec color) {tri(a,b,c,color);tri(a,c,d,color);}
    void box(Vec center,Vec half,Vec color) {
        Vec p[8];for(int i=0;i<8;i++)p[i]={center[0]+((i&1)?half[0]:-half[0]),center[1]+((i&2)?half[1]:-half[1]),center[2]+((i&4)?half[2]:-half[2])};
        for(auto face:std::array<std::array<int,4>,6>{{{0,1,3,2},{4,6,7,5},{0,4,5,1},{2,3,7,6},{0,2,6,4},{1,5,7,3}}})quad(p[face[0]],p[face[1]],p[face[2]],p[face[3]],color);
    }
    void label(char c,Vec origin,Vec color) {
        // Stroke glyphs replace TextMesh font rasterization; the scene geometry
        // and physical target dimensions remain identical to ImuDiagnostics.
        auto line=[&](float x,float y,float u,float v){
            // Match TextMesh's upper-left anchor and approximately its 72 px,
            // characterSize=.13 glyph extent; font rasterization differs.
            x*=1.6f;u*=1.6f;y=(y-.4f)*1.6f;v=(v-.4f)*1.6f;
            float dx=u-x,dy=v-y,n=std::sqrt(dx*dx+dy*dy),ox=-dy/n*.0288f,oy=dx/n*.0288f;
            quad({origin[0]+x+ox,origin[1]+y+oy,origin[2]},{origin[0]+u+ox,origin[1]+v+oy,origin[2]},{origin[0]+u-ox,origin[1]+v-oy,origin[2]},{origin[0]+x-ox,origin[1]+y-oy,origin[2]},color);};
        if(c=='X'){line(0,0,.25f,.4f);line(0,.4f,.25f,0);}
        if(c=='Y'){line(0,.4f,.125f,.2f);line(.25f,.4f,.125f,.2f);line(.125f,.2f,.125f,0);}
        if(c=='Z'){line(0,.4f,.25f,.4f);line(.25f,.4f,0,0);line(0,0,.25f,0);}
    }
    Scene() {
        const float distances[]={.6f,1,1.3f,1.6f},sizes[]={.2f,.4f,.4f,.4f},xs[]={-.35f,.5f,-.7f,.9f},ys[]={-.15f,-.2f,-.2f,-.2f};
        const Vec colors[]={{.3f,1,.35f},{.15f,.85f,.95f},{1,.7f,.1f},{.95f,.25f,.7f}};
        for(int i=0;i<4;i++)box({xs[i],ys[i],std::sqrt(distances[i]*distances[i]-xs[i]*xs[i]-ys[i]*ys[i])},{sizes[i]/2,sizes[i]/2,sizes[i]/2},colors[i]);
        for(int row=0;row<10;row++)for(int col=0;col<20;col++) {
            float x=(col-10)*.2f,y=(row-5)*.2f+.1f;
            quad({x,y,2.6f},{x+.2f,y,2.6f},{x+.2f,y+.2f,2.6f},{x,y+.2f,2.6f},(row+col)%2?Vec{.12f,.16f,.23f}:Vec{.9f,.9f,.9f});
        }
        const Vec red{1,0,0},green{0,1,0},blue{0,0,1};
        box({.325f,-.45f,3},{.325f,.0175f,.0175f},red);box({0,-.125f,3},{.0175f,.325f,.0175f},green);box({0,-.45f,3.325f},{.0175f,.0175f,.325f},blue);
        worldVertexCount=vertices.size();
        label('X',{.75f,-.45f,3},red);label('Y',{0,.3f,3},green);label('Z',{0,-.45f,3.75f},blue);
    }
};
static GLuint shader(GLenum type,const char* code) {
    GLuint s=glCreateShader(type);glShaderSource(s,1,&code,nullptr);glCompileShader(s);GLint ok;glGetShaderiv(s,GL_COMPILE_STATUS,&ok);
    if(!ok){char log[1024];glGetShaderInfoLog(s,sizeof(log),nullptr,log);ERROR("shader: %s",log);throw std::runtime_error("shader compilation");}return s;
}
struct Frame {
    EGLuint64KHR id;int64_t sequence,callback,sample,poseTarget,drawEnd;std::array<float,4> q;int64_t queryBoot;
    int64_t expected,deadline,vsyncId,requestSequence,drawStart,clockSpan;
    int generation; const char* predictionSource;
};
struct App {
    using DiagnosticGetter=int(*)(SpatialPoseDiagnostic*,int);
    DiagnosticGetter diagnosticGetter=nullptr;
    FILE* diagnosticsCsv=nullptr;
    CardboardHeadTracker* tracker=nullptr;CardboardDistortionRenderer* distortion=nullptr;
    std::array<Warp,2> meshes;GLuint fbo[2]{},texture[2]{},depth[2]{},program=0,vbo=0,vao=0;GLint mvpLocation=0;int vertexCount=0;
    int width=0,height=0,worldVertexCount=0;std::array<float,4> anchor{0,0,0,1},lastQ{0,0,0,1};bool reset=true,synthetic=false;
    int predictionMs=50;
    bool displayTime=false;
    int timingFrames=0,expiredFrames=0,fallbackFrames=0,deadlineMisses=0;
    double horizonSum=0,horizonMin=1e9,horizonMax=0;
    int64_t started=now(),lastDraw=0,lastReport=started,sequence=0;std::vector<double> intervals;
    FILE* csv=nullptr;std::deque<Frame> pending;bool timestamps=false,timestampCollection=false;int invalidFrames=0;int rows=0;
    PFNEGLGETNEXTFRAMEIDANDROIDPROC nextFrame=nullptr;PFNEGLGETFRAMETIMESTAMPSANDROIDPROC frameTimes=nullptr;
    PFNEGLGETFRAMETIMESTAMPSUPPORTEDANDROIDPROC supportsTime=nullptr;
    EGLDisplay display=EGL_NO_DISPLAY;EGLSurface surface=EGL_NO_SURFACE;
    void write(const Frame& f,int64_t present) {
        if(csv) {
            std::fprintf(csv,"%lld,%llu,%lld,%lld,%lld,%lld,%lld,%.7f,%.7f,%.7f,%.7f,%d,%d,%lld,",
                (long long)f.sequence,(unsigned long long)f.id,(long long)f.callback,(long long)f.sample,
                (long long)f.poseTarget,(long long)f.drawEnd,(long long)present,f.q[0],f.q[1],f.q[2],f.q[3],
                f.sample-started<5000000000LL?1:0,predictionMs,(long long)f.queryBoot);
            std::fprintf(csv,"%s,%s,%lld,%lld,%lld,%lld,%lld,%d,%lld,%lld,%d,%d\n",
                displayTime?"display-time":"fixed",f.predictionSource,(long long)(f.poseTarget-f.queryBoot),
                (long long)f.expected,(long long)f.deadline,(long long)f.vsyncId,(long long)f.requestSequence,
                f.generation,(long long)f.drawStart,(long long)f.clockSpan,
                f.deadline>0 && f.sample>f.deadline?1:0,f.deadline>0 && f.drawEnd>f.deadline?1:0);
            if(++rows%60==0)std::fflush(csv);
        }
    }
    void collect(bool finish=false) {
        while(!pending.empty()) {
            auto f=pending.front();EGLnsecsANDROID value=EGL_TIMESTAMP_INVALID_ANDROID;const EGLint token=EGL_DISPLAY_PRESENT_TIME_ANDROID;
            bool queried=frameTimes&&frameTimes(display,surface,f.id,1,&token,&value);
            if(queried&&value==EGL_TIMESTAMP_PENDING_ANDROID&&!finish&&sequence-f.sequence<120)break;
            if(!queried||value<=0){invalidFrames++;value=0;}
            write(f,value);pending.pop_front();
        }
    }
    ~App() {
        if(diagnosticsCsv){std::fflush(diagnosticsCsv);std::fclose(diagnosticsCsv);}
        if(tracker){CardboardHeadTracker_pause(tracker);CardboardHeadTracker_destroy(tracker);}
        collect(true);if(csv){std::fflush(csv);std::fclose(csv);}
        if(distortion)CardboardDistortionRenderer_destroy(distortion);
        glDeleteFramebuffers(2,fbo);glDeleteTextures(2,texture);glDeleteRenderbuffers(2,depth);glDeleteBuffers(1,&vbo);glDeleteVertexArrays(1,&vao);if(program)glDeleteProgram(program);
        INFO("RELEASED tracker=true csvRows=%d invalidPresent=%d",rows,invalidFrames);
    }
    void resize(int w,int h) {
        if(w!=kWidth||h!=kHeight)throw std::runtime_error("Surface dimensions differ from captured calibration; refusing scaled optics");
        if(width==w&&height==h)return;width=w;height=h;
        glGenFramebuffers(2,fbo);glGenTextures(2,texture);glGenRenderbuffers(2,depth);
        for(int e=0;e<2;e++) {
            glBindTexture(GL_TEXTURE_2D,texture[e]);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_S,GL_CLAMP_TO_EDGE);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_T,GL_CLAMP_TO_EDGE);
            glTexImage2D(GL_TEXTURE_2D,0,GL_RGB,w/2,h,0,GL_RGB,GL_UNSIGNED_BYTE,nullptr);
            glBindRenderbuffer(GL_RENDERBUFFER,depth[e]);glRenderbufferStorage(GL_RENDERBUFFER,GL_DEPTH_COMPONENT16,w/2,h);
            glBindFramebuffer(GL_FRAMEBUFFER,fbo[e]);glFramebufferTexture2D(GL_FRAMEBUFFER,GL_COLOR_ATTACHMENT0,GL_TEXTURE_2D,texture[e],0);glFramebufferRenderbuffer(GL_FRAMEBUFFER,GL_DEPTH_ATTACHMENT,GL_RENDERBUFFER,depth[e]);
            if(glCheckFramebufferStatus(GL_FRAMEBUFFER)!=GL_FRAMEBUFFER_COMPLETE)throw std::runtime_error("incomplete eye framebuffer");
        }
        INFO("SURFACE width=%d height=%d eyeTexture=%dx%d rects=%s",w,h,w/2,h,kRectsText);
    }
    void draw(int64_t callback,int64_t expected,int64_t deadline,int64_t vsyncId,int64_t requestSequence,int generation) {
        if(!width)return;const int64_t sample=now();sequence++;
        ATrace_beginSection("SpatialNative.draw");
        // Present-fence capability may become available only after the first
        // buffers reach SurfaceFlinger. Enable collection first, then recheck.
        if(!timestamps&&timestampCollection&&sequence%60==0) {
            timestamps=supportsTime(display,surface,EGL_DISPLAY_PRESENT_TIME_ANDROID);
            if(sequence<=180||timestamps)INFO("TIMESTAMP_CAPABILITY collectionEnabled=%d presentSupported=%d eglError=0x%x",timestampCollection,timestamps,eglGetError());
        }
        collect();
        if(lastDraw)intervals.push_back((sample-lastDraw)/1e6);lastDraw=sample;
        float p[3]{},q[4]{};
        const int64_t monoBefore=now(),queryBoot=now(CLOCK_BOOTTIME),monoAfter=now();
        const int64_t queryMono=monoBefore+(monoAfter-monoBefore)/2;
        const auto timing=selectPoseTiming(displayTime,predictionMs,queryMono,queryBoot,callback,expected,deadline);
        const int64_t target=timing.targetBoot;
        ATrace_beginSection("SpatialNative.getPose");
        const int64_t poseStart=now();
        CardboardHeadTracker_getPose(tracker,target,kLandscapeLeft,p,q);
        const int64_t poseEnd=now();
        ATrace_endSection();
        SpatialPoseDiagnostic diagnostic{};
        const bool diagnosticValid=diagnosticGetter && diagnosticGetter(&diagnostic,sizeof(diagnostic)) &&
            diagnostic.version==1 && diagnostic.targetBoot==target && diagnostic.sampleBoot>0;
        const double horizonMs=(target-queryBoot)/1e6;
        timingFrames++;horizonSum+=horizonMs;horizonMin=std::min(horizonMin,horizonMs);horizonMax=std::max(horizonMax,horizonMs);
        if(std::strcmp(timing.source,"expired_clamp_zero")==0)expiredFrames++;
        if(std::strncmp(timing.source,"fallback_",9)==0)fallbackFrames++;
        // Match CardboardRotationToUnityPose, then the existing host mapping.
        q[2]=-q[2];CardboardUnity_applyHostTracking(p,q);
        lastQ={q[0],q[1],q[2],q[3]};
        if(reset||sequence==30){anchor=lastQ;reset=false;}
        Matrix relative=multiply(inverseRotation(rotation(lastQ)),rotation(anchor));
        if(synthetic){float angle=.436332313f*std::sin((sample-started)/1e9*1.570796327);relative=rotation({0,std::sin(angle/2),0,std::cos(angle/2)});}
        auto handed=identity();handed[10]=-1;auto view=multiply(handed,relative);
        const float n=.05f,far=1000.f,left=kFrustumLeft/kNear*n,right=kFrustumRight/kNear*n,bottom=kBottom/kNear*n,top=kTop/kNear*n;
        Matrix projection{};projection[0]=2*n/(right-left);projection[5]=2*n/(top-bottom);projection[8]=(right+left)/(right-left);projection[9]=(top+bottom)/(top-bottom);projection[10]=-(far+n)/(far-n);projection[11]=-1;projection[14]=-2*far*n/(far-n);
        EGLuint64KHR id=0;if(timestamps&&!nextFrame(display,surface,&id))id=0;
        for(int e=0;e<2;e++) {
            glBindFramebuffer(GL_FRAMEBUFFER,fbo[e]);glViewport(0,0,width/2,height);glDisable(GL_SCISSOR_TEST);glEnable(GL_DEPTH_TEST);glDisable(GL_CULL_FACE);glDisable(GL_BLEND);glClearColor(.015f,.025f,.055f,1);glClear(GL_COLOR_BUFFER_BIT|GL_DEPTH_BUFFER_BIT);
            auto eye=identity();eye[12]=(e==0?1:-1)*kBaseline*.5f;auto mvp=multiply(projection,multiply(eye,view));
            glUseProgram(program);glUniformMatrix4fv(mvpLocation,1,GL_FALSE,mvp.data());glBindVertexArray(vao);glDrawArrays(GL_TRIANGLES,0,worldVertexCount);
            // Unity's default TextMesh uses an overlay text material.
            glDisable(GL_DEPTH_TEST);glDrawArrays(GL_TRIANGLES,worldVertexCount,vertexCount-worldVertexCount);
        }
        glBindVertexArray(0);glBindFramebuffer(GL_FRAMEBUFFER,0);glViewport(0,0,width,height);glDisable(GL_DEPTH_TEST);glClearColor(0,0,0,1);glClear(GL_COLOR_BUFFER_BIT);
        CardboardEyeTextureDescription eyes[2]={{texture[0],0,1,1,0},{texture[1],0,1,1,0}};
        CardboardDistortionRenderer_renderEyeToDisplay(distortion,0,0,0,width,height,&eyes[0],&eyes[1]);
        Frame frame{id,sequence,callback,queryMono,target,now(),lastQ,queryBoot,
            expected,deadline,vsyncId,requestSequence,sample,monoAfter-monoBefore,generation,timing.source};
        if(deadline>0 && frame.drawEnd>deadline)deadlineMisses++;
        if(id)pending.push_back(frame);else write(frame,0);
        if(diagnosticsCsv) {
            std::fprintf(diagnosticsCsv,"%lld,%lld,%d,%d,%lld,%lld,%lld,%lld,%lld,%lld,%lld,%lld,%lld,%lld",
                (long long)sequence,(long long)requestSequence,generation,diagnosticValid?1:0,
                (long long)queryMono,(long long)diagnostic.queryBoot,(long long)diagnostic.sampleBoot,
                (long long)diagnostic.fusionReceiveBoot,(long long)target,(long long)poseStart,(long long)poseEnd,
                (long long)sample,(long long)frame.drawEnd,(long long)expected);
            for(double v:diagnostic.velocity)std::fprintf(diagnosticsCsv,",%.9f",v);
            for(double v:diagnostic.baseQuaternion)std::fprintf(diagnosticsCsv,",%.9f",v);
            for(double v:diagnostic.predictedQuaternion)std::fprintf(diagnosticsCsv,",%.9f",v);
            std::fprintf(diagnosticsCsv,",%d\n",sample-started<5000000000LL?1:0);
            if(sequence%120==0)std::fflush(diagnosticsCsv);
        }
        if(sample-lastReport>=5000000000LL&&!intervals.empty()) {
            std::sort(intervals.begin(),intervals.end());auto percentile=[&](double f){return intervals[std::max(0,(int)std::ceil(f*intervals.size())-1)];};
            INFO("WINDOW n=%zu renderP95Ms=%.3f renderP99Ms=%.3f maxMs=%.3f presentSupported=%d pending=%zu invalidPresent=%d motion=%s predictionMs=%d",intervals.size(),percentile(.95),percentile(.99),intervals.back(),timestamps,pending.size(),invalidFrames,synthetic?"synthetic":"imu",predictionMs);
            INFO("POSE_TIMING mode=%s n=%d horizonMeanMs=%.3f minMs=%.3f maxMs=%.3f expired=%d fallback=%d cpuDeadlineMiss=%d expectedIsActual=false",
                displayTime?"display-time":"fixed",timingFrames,horizonSum/timingFrames,horizonMin,horizonMax,expiredFrames,fallbackFrames,deadlineMisses);
            timingFrames=expiredFrames=fallbackFrames=deadlineMisses=0;horizonSum=0;horizonMin=1e9;horizonMax=0;
            INFO("SENSOR_DIAGNOSTIC valid=%d sampleAgeMs=%.3f sampleToFusionMs=%.3f poseCpuMs=%.3f drawCpuMs=%.3f actualPresentAvailable=%d",
                diagnosticValid,diagnosticValid?(diagnostic.queryBoot-diagnostic.sampleBoot)/1e6:-1.,
                diagnosticValid?(diagnostic.fusionReceiveBoot-diagnostic.sampleBoot)/1e6:-1.,
                (poseEnd-poseStart)/1e6,(frame.drawEnd-sample)/1e6,timestamps);
            intervals.clear();lastReport=sample;if(csv)std::fflush(csv);
        }
        ATrace_endSection();
    }
};
static bool sdkInitialized=false;
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_samples_cardboardnative_MainActivity_nativeInitialize(JNIEnv*env,jobject activity) {
    if(sdkInitialized)return;JavaVM* vm;env->GetJavaVM(&vm);Cardboard_initializeAndroid(vm,activity);
    if(!env->ExceptionCheck())sdkInitialized=true;
}
extern "C" JNIEXPORT jlong JNICALL Java_com_spatialai_samples_cardboardnative_MainActivity_nativeCreate(JNIEnv*env,jobject,jobject,jstring path,jboolean synthetic,jint predictionMs,jboolean displayTime) {
    App* app=nullptr;
    try {
        if(!sdkInitialized)throw std::runtime_error("SDK must be initialized on the Activity thread");
        if(predictionMs<0 || predictionMs>100)throw std::runtime_error("predictionMs outside 0..100");
        app=new App();app->synthetic=synthetic;app->predictionMs=predictionMs;app->displayTime=displayTime;
        if(!CardboardUnity_configureHostTracking(kBasis[0],kBasis[1],kBasis[2],kBasis[3],kMount[0],kMount[1],kMount[2],kMount[3],0,0))throw std::runtime_error("invalid host mapping");
        CardboardUnity_setHostViewCompensation(kInvertPitch,kInvertRoll);
        app->tracker=CardboardHeadTracker_create();if(!app->tracker)throw std::runtime_error("no head tracker");CardboardHeadTracker_resume(app->tracker);
        CardboardOpenGlEsDistortionRendererConfig config{kGlTexture2D};app->distortion=CardboardOpenGlEs3DistortionRenderer_create(&config);
        if(!app->distortion)throw std::runtime_error("no distortion renderer");
        for(int e=0;e<2;e++){app->meshes[e]=warp(e);auto&m=app->meshes[e];m.mesh={m.indices.data(),(int)m.indices.size(),m.xy.data(),m.uv.data(),(int)m.xy.size()/2};CardboardDistortionRenderer_setMesh(app->distortion,&m.mesh,e?kRight:kLeft);}
        const char* vertex="#version 300 es\n layout(location=0) in vec3 p; layout(location=1) in vec3 c; uniform mat4 mvp; out vec3 color; void main(){gl_Position=mvp*vec4(p,1);color=c;}";
        const char* fragment="#version 300 es\n precision mediump float; in vec3 color; out vec4 outColor; void main(){outColor=vec4(color,1);}";
        GLuint vs=shader(GL_VERTEX_SHADER,vertex),ps=shader(GL_FRAGMENT_SHADER,fragment);app->program=glCreateProgram();glAttachShader(app->program,vs);glAttachShader(app->program,ps);glLinkProgram(app->program);glDeleteShader(vs);glDeleteShader(ps);
        GLint linked;glGetProgramiv(app->program,GL_LINK_STATUS,&linked);if(!linked)throw std::runtime_error("program link");app->mvpLocation=glGetUniformLocation(app->program,"mvp");
        Scene scene;app->vertexCount=scene.vertices.size();app->worldVertexCount=scene.worldVertexCount;glGenVertexArrays(1,&app->vao);glBindVertexArray(app->vao);glGenBuffers(1,&app->vbo);glBindBuffer(GL_ARRAY_BUFFER,app->vbo);glBufferData(GL_ARRAY_BUFFER,scene.vertices.size()*sizeof(Vertex),scene.vertices.data(),GL_STATIC_DRAW);glEnableVertexAttribArray(0);glVertexAttribPointer(0,3,GL_FLOAT,GL_FALSE,sizeof(Vertex),(void*)0);glEnableVertexAttribArray(1);glVertexAttribPointer(1,3,GL_FLOAT,GL_FALSE,sizeof(Vertex),(void*)(3*sizeof(float)));glBindVertexArray(0);
        app->display=eglGetCurrentDisplay();app->surface=eglGetCurrentSurface(EGL_DRAW);
        app->nextFrame=(PFNEGLGETNEXTFRAMEIDANDROIDPROC)eglGetProcAddress("eglGetNextFrameIdANDROID");app->frameTimes=(PFNEGLGETFRAMETIMESTAMPSANDROIDPROC)eglGetProcAddress("eglGetFrameTimestampsANDROID");
        app->supportsTime=(PFNEGLGETFRAMETIMESTAMPSUPPORTEDANDROIDPROC)eglGetProcAddress("eglGetFrameTimestampsSupportedANDROID");
        const char* extensions=eglQueryString(app->display,EGL_EXTENSIONS);
        app->timestampCollection=extensions&&std::strstr(extensions,"EGL_ANDROID_get_frame_timestamps")&&app->nextFrame&&app->frameTimes&&app->supportsTime&&eglSurfaceAttrib(app->display,app->surface,EGL_TIMESTAMPS_ANDROID,EGL_TRUE);
        app->timestamps=app->timestampCollection&&app->supportsTime(app->display,app->surface,EGL_DISPLAY_PRESENT_TIME_ANDROID);
        const char* filename=env->GetStringUTFChars(path,nullptr);app->csv=std::fopen(filename,"a");
        app->diagnosticGetter=reinterpret_cast<App::DiagnosticGetter>(dlsym(RTLD_DEFAULT,"CardboardDebug_getLastPoseDiagnostic"));
        if(app->diagnosticGetter) {
            std::string diagnosticPath=std::string(filename)+".diagnostics.csv";
            app->diagnosticsCsv=std::fopen(diagnosticPath.c_str(),"a");
            if(!app->diagnosticsCsv)throw std::runtime_error("cannot open sensor diagnostics CSV");
            std::setvbuf(app->diagnosticsCsv,nullptr,_IOFBF,65536);std::fseek(app->diagnosticsCsv,0,SEEK_END);
            if(std::ftell(app->diagnosticsCsv)==0)std::fprintf(app->diagnosticsCsv,
                "sequence,request_sequence,refresh_generation,valid,query_mono_ns,sdk_query_boot_ns,sample_boot_ns,fusion_receive_boot_ns,target_boot_ns,pose_start_mono_ns,pose_end_mono_ns,draw_start_mono_ns,draw_end_mono_ns,expected_present_mono_ns,wx,wy,wz,base_qx,base_qy,base_qz,base_qw,pred_qx,pred_qy,pred_qz,pred_qw,warmup\n");
        }
        INFO("DIAGNOSTICS sdkSnapshot=%d traceSections=SpatialNative.draw,SpatialNative.getPose",app->diagnosticGetter!=nullptr);
        INFO("CONFIG revision=native-cardboard-diagnostics-v6 targetFpsSource=DISPLAY_RATE predictionMs=%d predictionMode=%s signedBaseline=%.3f meshHash=%s calibration=%s motion=%s presentSupported=%d csv=%s",predictionMs,displayTime?"display-time":"fixed",kBaseline,kMeshHash,kCalibrationHash,synthetic?"synthetic":"imu",app->timestamps,filename);
        env->ReleaseStringUTFChars(path,filename);
        if(!app->csv)throw std::runtime_error("cannot open timeline CSV");
        std::setvbuf(app->csv,nullptr,_IOFBF,65536);std::fseek(app->csv,0,SEEK_END);
        if(std::ftell(app->csv)==0)std::fprintf(app->csv,"sequence,egl_frame_id,callback_mono_ns,pose_query_mono_ns,pose_target_boot_ns,draw_end_mono_ns,present_mono_ns,qx,qy,qz,qw,warmup,prediction_ms,pose_query_boot_ns,prediction_mode,prediction_source,effective_prediction_ns,expected_present_mono_ns,deadline_mono_ns,vsync_id,request_sequence,refresh_generation,draw_start_mono_ns,clock_sample_span_ns,query_deadline_missed,draw_deadline_missed\n");app->intervals.reserve(1024);
        return reinterpret_cast<jlong>(app);
    }catch(const std::exception&e){ERROR("INIT FAILED %s",e.what());delete app;return 0;}
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_samples_cardboardnative_MainActivity_nativeResize(JNIEnv*,jclass,jlong handle,jint w,jint h){try{reinterpret_cast<App*>(handle)->resize(w,h);}catch(const std::exception&e){ERROR("SURFACE FAILED %s",e.what());}}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_samples_cardboardnative_MainActivity_nativeDraw(JNIEnv*,jclass,jlong handle,jlong callback,jlong expected,jlong deadline,jlong vsyncId,jlong requestSequence,jint generation){reinterpret_cast<App*>(handle)->draw(callback,expected,deadline,vsyncId,requestSequence,generation);}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_samples_cardboardnative_MainActivity_nativeReset(JNIEnv*,jclass,jlong handle){reinterpret_cast<App*>(handle)->reset=true;INFO("RESET sceneAnchor=requested");}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_samples_cardboardnative_MainActivity_nativeDestroy(JNIEnv*,jclass,jlong handle){delete reinterpret_cast<App*>(handle);}
