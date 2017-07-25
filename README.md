# ViveTrackingTest_HoloVersion
Three voice commands should be called in sequence
"rotation": cube's rotation right, match the real one; if not matched, call it again to adjust
"origin" : put the surgical desk's origin to the cube's center's position and 15cm below, same rotation
"store": store the surgical desk's position relative to the vive meter
(The master branch apply the newly implemented smooth mechanism, to see calibration without smoothing, see branch without_smoothing)

The port number for Hololens is 9001. 
When the tracking is not working, the UDP sender will send default data of position (x:0, y:0, z:0) rotation (x:0, y:0, z:0, w:1)
