# Third-party contents in the Unity preview bundle

The Spatial-modified Google Cardboard package retains upstream LICENSE.md (Apache-2.0) and source copyright notices. Its Android ARM64 native library includes the Spatial display/IMU patch and RhinoX reference mesh. It is a modified distribution; upstream version alone does not identify its contents. The release manifest records upstream revisions and patch/native/AAR hashes.

The hand AAR is built from spatial-ai-android and contains hand_landmarker.task and gesture_recognizer.task. Their exact checksums and MediaPipe dependency version are recorded in the release manifest. MediaPipe and UVC Android dependencies are resolved by Gradle from their upstream Maven repositories; they are not copied as a separate offline dependency repository in this bundle. Preserve their upstream attribution and transitive license files when redistributing an application.

Unity XR Hands, Input System, XR Management and XRI are resolved through Unity Package Manager under their upstream package licenses. The official XR Hands Gestures sample is imported unchanged from the installed Unity package by the consumer; the Spatial package supplies shape assets and prefab wiring.

The release is a local development preview. No new open-source license is assigned to Spatial-owned code or reference device assets by this packaging workflow. External publishing and license terms remain the project owner's release process.
