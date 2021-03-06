﻿#if UNITY_WSA_10_0 && !UNITY_EDITOR
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
#else
using System.Net.Sockets;
#endif

using System.Net;

using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.IO;

[Serializable]
public class TrackerTransform
{
    public string id;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 cam_position;
    public Quaternion cam_rotation;

    public TrackerTransform() { id = "unspecified"; position = new Vector3(); rotation = new Quaternion(); cam_position = new Vector3();
        cam_rotation = new Quaternion(); }
    public TrackerTransform(string id_, Vector3 pos, Quaternion quat) { id = id_; position = pos; rotation = quat; cam_position = new Vector3();
        cam_rotation = new Quaternion();
    }
}

public class HoloReceiver : MonoBehaviour
{

    public int port = 9001;
    public bool follow = false;
    public string TrackerID = "tracker1";


    [Range(0f, 100.0f)]
    public float output_rate;// = 0.2f;
    private Quaternion from0toTable;
    Quaternion re_from0toTable;
    private Vector3 meter_position;
    private Quaternion meter_rot;
    private Quaternion camera_rot = new Quaternion(0, 0, 0, 1);
    private Quaternion rot_calib;//need to be rotated before other rotation.
    private bool calibrating = false;
    private bool calib_confirmed = false;
    private bool first_tracking = true;
    private GameObject calibratedCam;
    private GameObject calibratedCamChild;
    private GameObject tableParent;
    private GameObject tableChild;
    private GameObject storedTable;
    private Vector3 storedTablePos;
    private Quaternion storedTableRot;
    private Vector3 counter_drift_total;
    private Vector3 previous_drift_amount;
    private int drift_accum_curr;
    private HoloClient Instance { get; set; }
    // Use this for initialization

	public GameObject ViveEmitter;
	public GameObject ViveHoloTracker;
	public GameObject ViveCASTTracker;
	public GameObject CASTOrigin;
	bool ViveEmitter_calibrated = false;
	Vector3 ViveToHoloPos = new Vector3(0,0,0);
	Quaternion ViveToHoloRot = new Quaternion(0,0,0,1);
	bool ViveToHolo_calibrated = false;
	//Debug:
	public GameObject ViveCASTTrackerDebug;


    void Start()
    {
        this.Instance = this.gameObject.AddComponent<HoloClient>();
        this.Instance.Init(port, output_rate);
       
        from0toTable = new Quaternion(0.666f, -0.230f, 0.230f, 0.671f);
        re_from0toTable = Quaternion.Inverse(from0toTable);

        calibratedCam = new GameObject("calibratedCam");
        calibratedCam.transform.position = new Vector3();
        calibratedCam.transform.rotation = new Quaternion();
        calibratedCamChild = new GameObject("calibratedCamChild");
        calibratedCamChild.transform.parent = calibratedCam.transform;
        tableParent = new GameObject("calibratedTableParent");
        tableParent.transform.position = new Vector3();
        tableParent.transform.rotation = new Quaternion();
        tableChild = new GameObject("calibratedTableOrigin");
        tableChild.transform.parent = tableParent.transform;

        Vector3 pos = new Vector3(-0.134f, -0.977f, -0.906f); //origin
        //pos = new Vector3(-0.284f, -0.967f, -1.01f);//point2
        //pos = new Vector3(-0.347f,-0.787f,-1.046f);//point3
        //pos = new Vector3(-0.346f, -0.966f, -0.933f);//point4
        //pos = new Vector3(-0.545f, -0.962f, -0.676f);//point5
        //Vector3 pos2 = this.rotateAroundAxis(pos, new Vector3(0, 0, 0), pointQuat);
        //Debug.Log("pos2 x: " + pos2.x + " y: " + pos2.y + " z: " + pos2.z);


        Vector3 mid_pos = this.rotateAroundAxis(pos, new Vector3(0, 0, 0), re_from0toTable);
        Vector3 final_pos = new Vector3(mid_pos.x, mid_pos.z, -mid_pos.y);
        //this.transform.localPosition = final_pos;
        //this.transform.parent.Find("ViveMeter").localPosition = final_pos;
        meter_position = final_pos;

        Vector3 init_pos = new Vector3(0.134f, -0.977f, -0.906f);
        Vector3 end_pos = new Vector3(0.6551008f, -0.9704683f, -0.6384149f);
        meter_rot = Quaternion.FromToRotation(init_pos, end_pos);
        //this.transform.parent.Find("ViveMeter").localRotation = meter_rot;

        Quaternion re_rot = new Quaternion(from0toTable.x, from0toTable.z, -from0toTable.y, from0toTable.w);
        rot_calib = Quaternion.Inverse(meter_rot * re_rot);

       Debug.Log("HoloReceiver Started");
        // this.enabled = false;

		/* GIOVA*/
		if(!ViveEmitter)
			ViveEmitter = GameObject.Find("ViveEmitter");
		if (!ViveCASTTracker)
			ViveCASTTracker = GameObject.Find("ViveTracker");
		if (!ViveHoloTracker)
			ViveHoloTracker = GameObject.Find("HoloTracker");
		if(!ViveCASTTrackerDebug)
			ViveCASTTrackerDebug = GameObject.Find("ViveTrackerFromHolo");
		if (!CASTOrigin)
			CASTOrigin = GameObject.Find ("Origin");
    }
    Vector3 rotateAroundAxis(Vector3 point, Vector3 pivot, Quaternion quat)
    {
        Vector3 finalPos = point - pivot;
        finalPos = quat * finalPos;
        finalPos += pivot;
        return finalPos;
    }

    // Update is called once per frame
    void Update()
    {
        //TrackerTransform tt = new TrackerTransform();
        //string st = JsonUtility.ToJson(tt);
        ////Debug.Log("UPDATE");
        //Debug.Log(st);

		if (Input.GetKeyDown(KeyCode.E) && !calibrating)
		{
			Debug.Log("E pressed");
			this.CalibrateViveEmitter ();
		}
        if (Input.GetKeyDown(KeyCode.C) && !calibrating)
        {
			CalibrateViveWithHololens();
            Debug.Log("C pressed");
        }
        if (Input.GetKeyDown(KeyCode.S) && !calibrating)
        {
            this.StoreTableCalibration();
            Debug.Log("S pressed");
        }
        if (Input.GetKeyDown(KeyCode.R) && !calibrating)
        {
            this.CalibrateRotation();
            Debug.Log("R pressed");
        }
        
        if (Input.GetKeyDown(KeyCode.H) && !calibrating)
        {
           // this.StartCalibrateHolo();
            Debug.Log("H pressed");
        }
        //if (Instance.bTT)
        {
            //
            //Debug.Log("Applying Tracker Update");

            //this.transform.position = Instance.lastTrackerTransform.position;
            //this.transform.rotation = Instance.lastTrackerTransform.rotation;

            //Camera.main.transform.position = Instance.lastTrackerTransform.position;
            //Camera.main.transform.rotation = Instance.lastTrackerTransform.rotation;

            //Debug.Log("Cam  " + Camera.main.transform.position.ToString());
            //Debug.Log("CamF " + FakeCamera.transform.position.ToString());
            //Debug.Log("Root " + transform.position.ToString());
        }

        //if (Instance.bTT_1 && Instance.bTT_0 && !calibrating)
        //{
        //    //Vector3 pos = Instance.lastTrackerTransform.position;
        //    //pos = new Vector3(-pos.x, pos.y, pos.z);
        //    ////Vector3 hmdPos = Quaternion.Euler(90, 0, 0) * pos;
        //    //Vector3 hmdPos = pos + new Vector3(0f, .0f, 3.0f);
        //    ////hmdPos.x *= -1.0f;
        //    //this.transform.localPosition = hmdPos;
        //    ////Debug.Log("Pos " + Instance.lastTrackerTransform.position + " - " + hmdPos);

        //    ////Quaternion rot = Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(Instance.lastTrackerTransform.rotation);
        //    //Quaternion rot = (Instance.lastTrackerTransform.rotation);
        //    //rot.x *= - 1.0f;
        //    //rot.y *= -1.0f;
        //    ////rot.z *= -1.0f;
        //    //rot = new Quaternion(rot.x, rot.y, rot.z, rot.w);
        //    //Quaternion quatB = Quaternion.AngleAxis(90, Vector3.right);
        //    //Quaternion quatC = Quaternion.AngleAxis(180, Vector3.up);
        //    //rot =  quatC *rot * quatB * quatC;
        //    //Vector3 hmdRot = rot.eulerAngles;
        //    ////hmdRot.x *= - 1.0f;
        //    //Debug.Log("Rot " + Instance.lastTrackerTransform.rotation.eulerAngles + " - " + hmdRot);
        //    //this.transform.localRotation = Quaternion.Euler(hmdRot);



        //    Vector3 pos = Instance.lastTrackerTransform_1.position;
        //    Vector3 mid_pos = this.rotateAroundAxis(pos, new Vector3(0, 0, 0), re_from0toTable);
        //    //position relative to vive meter
        //    Vector3 mid2_pos = new Vector3(-mid_pos.x, -mid_pos.z, mid_pos.y);
        //    //Vector3 final_pos = meter_position + mid2_pos;
        //    //Vector3 hmdPos = Quaternion.Euler(90, 0, 0) * pos;
        //    //this.transform.localPosition = final_pos;

        //    //Debug.Log("Pos " + Instance.lastTrackerTransform.position + " - " + hmdPos);


        //    Vector3 stablePosCalib = new Vector3(0, 0.055f, 0);

        //    //Transform localToCamera = Camera.main.transform.Find("TrackerLocalToCamera");
        //    //localToCamera.position = meter_position;
        //    //localToCamera.localPosition = localToCamera.localPosition + mid2_pos;
        //    //this.transform.position = localToCamera.position;

        //    Transform calibratedChildTrans = calibratedCamChild.transform;
        //    calibratedChildTrans.position = meter_position;
        //    calibratedChildTrans.localPosition = calibratedChildTrans.localPosition + mid2_pos;
        //    this.transform.position = calibratedChildTrans.position;


        //    //Quaternion rot = Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(Instance.lastTrackerTransform.rotation);
        //    Quaternion rot = (Instance.lastTrackerTransform_1.rotation);

        //    //version1 by far best (y axis works)
        //    //rot = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
        //    //Quaternion re_from0toTable_left = new Quaternion(re_from0toTable.x, -re_from0toTable.y, -re_from0toTable.z, re_from0toTable.w);

        //    //version2 not working
        //    //rot = new Quaternion(rot.x, -rot.z, -rot.y, rot.w);
        //    //Quaternion re_from0toTable_left = new Quaternion(re_from0toTable.x, -re_from0toTable.z, -re_from0toTable.y, re_from0toTable.w);

        //    //version3 not working
        //    //rot = new Quaternion(-rot.x, rot.y, -rot.z, rot.w);
        //    //Quaternion re_from0toTable_left = new Quaternion(-re_from0toTable.x, re_from0toTable.y, -re_from0toTable.z, re_from0toTable.w);

        //    //version4
        //    rot = new Quaternion(rot.x, rot.z, -rot.y, rot.w);
        //    //Quaternion re_rot = new Quaternion(0.666f, 0.230f, 0.230f, 0.671f);
        //    //Quaternion re_from0toTable_left = new Quaternion(re_from0toTable.x, -re_from0toTable.y, -re_from0toTable.z, re_from0toTable.w);

        //    //Quaternion change = (Quaternion.Inverse(meter_rot * re_rot) * meter_rot * rot);
        //    Quaternion change = rot_calib * meter_rot * rot;
        //    //Quaternion change_left = new Quaternion(-change.x, -change.y, -change.z, change.w);
        //    //Debug.Log(change);
        //    this.transform.localRotation = camera_rot * change;
        //}

        if (Instance.bTT_0 && Instance.bTT_1 && !calibrating)
        {
            TrackerTransform TT0 = Instance.lastTrackerTransform_0;
            TrackerTransform TT1 = Instance.lastTrackerTransform_1;
            if(TT0.position.x == 0 && TT0.position.y == 0 && TT0.position.z == 0 && TT0.rotation.x == 0 && TT0.rotation.y == 0 && TT0.rotation.z == 0 && TT0.rotation.w == 1.0f)
            {
                //Tracker0 Hololens one lose tracking
                Debug.Log("Tracker 0 lost tracking");
                this.GetComponent<Renderer>().material.color = Color.blue;
                first_tracking = true;
                return;
            }
            else
            {
                this.GetComponent<Renderer>().material.color = Color.white;
            }
            if (TT1.position.x == 0 && TT1.position.y == 0 && TT1.position.z == 0 && TT1.rotation.x == 0 && TT1.rotation.y == 0 && TT1.rotation.z == 0 && TT1.rotation.w == 1.0f)
            {
                //Tracker1 lose tracking
                Debug.Log("Tracker 1 lost tracking");
                this.GetComponent<Renderer>().material.color = Color.red;
                first_tracking = true;
                return;
            }
            else
            {
                this.GetComponent<Renderer>().material.color = Color.white;
            }
        }
        if (//Instance.bTT_0 && 
			Instance.bTT_1 
			&& !calibrating)
        {
			//Vive emitter transform according to tracker 1
			if (!ViveEmitter_calibrated) {
				UpdateViveEmitterCalibration ();
			}

			UpdateViveHoloTracker ();


			//CalibrateViveWithHololens();

			/* START
            TrackerTransform TT0 = Instance.lastTrackerTransform_0;
            TrackerTransform TT1 = Instance.lastTrackerTransform_1;
            Vector3 stablePosCalib = new Vector3(0, 0.1f, 0.02f);
            //meter raw data
            Vector3 m_pos_raw = TT0.position;
            Quaternion m_rot = TT0.rotation;
            //tracker2 raw data
            Vector3 t_pos_raw = TT1.position;
            Quaternion t_rot = TT1.rotation;
            //set camera position
            calibratedCam.transform.position = TT0.cam_position;
            calibratedCam.transform.rotation = TT0.cam_rotation;

            //set meter position relative to Hololens
            Quaternion re_m_rot = Quaternion.Inverse(m_rot);
            Vector3 mid_pos = this.rotateAroundAxis(m_pos_raw, new Vector3(0, 0, 0), re_m_rot);
            //Vector3 m_final_pos = new Vector3(mid_pos.x, mid_pos.z, -mid_pos.y);
            //Transform localToCamera = Camera.main.transform.Find("TrackerLocalToCamera");
            //localToCamera.localPosition = m_final_pos + stablePosCalib;
            //meter_position = localToCamera.position;
            

            if (calib_confirmed)
            {
                Vector3 m_final_pos = new Vector3(mid_pos.x, mid_pos.z, -mid_pos.y);
                Transform localToCamera = Camera.main.transform.Find("TrackerLocalToCamera");
                localToCamera.localPosition = m_final_pos + stablePosCalib;
                //smooth the drift amount by specified number
                Vector3 counter_drift_amount = localToCamera.position - meter_position;
                if (first_tracking)
                {
                    previous_drift_amount = counter_drift_amount;
                    counter_drift_total += counter_drift_amount;
                    drift_accum_curr++;
                    first_tracking = false;
                }
                else
                {
                    if (Math.Abs((counter_drift_amount-previous_drift_amount).magnitude) <= 0.05f)
                    {
                        //Adding a threshold to the difference two continuous data;
                        drift_accum_curr++;
                        previous_drift_amount = counter_drift_amount;
                        counter_drift_total += counter_drift_amount;
                    }
                }
                
               
                
                if (drift_accum_curr >= 30)
                {
                    Vector3 counter_drift_avg = counter_drift_total / 30;
                    meter_position = meter_position + counter_drift_avg;
                    GameObject.Find("ViveMeter").transform.position = meter_position;
                    drift_accum_curr = 0;
                    counter_drift_total = new Vector3(0, 0, 0);
                }

                storedTable.transform.localPosition = storedTablePos;
                storedTable.transform.localRotation = storedTableRot;
                GameObject.Find("Origin").transform.rotation = storedTable.transform.rotation;
                GameObject.Find("Origin").transform.position = storedTable.transform.position;
            }
            else
            {
                Vector3 m_final_pos = new Vector3(mid_pos.x, mid_pos.z, -mid_pos.y);
                Transform localToCamera = Camera.main.transform.Find("TrackerLocalToCamera");
                localToCamera.localPosition = m_final_pos + stablePosCalib;
                meter_position = localToCamera.position;
                GameObject.Find("ViveMeter").transform.position = meter_position;
            }

            //set tracker position
            mid_pos = this.rotateAroundAxis(t_pos_raw, new Vector3(0, 0, 0), re_m_rot);
            //position relative to vive meter
            Vector3 mid2_pos = new Vector3(-mid_pos.x, -mid_pos.z, mid_pos.y);
            Transform calibratedChildTrans = calibratedCamChild.transform;
            calibratedChildTrans.position = meter_position;
            calibratedChildTrans.localPosition = calibratedChildTrans.localPosition + mid2_pos;
            this.transform.position = calibratedChildTrans.position;

            //set tracker rotation
            Quaternion rot = TT1.rotation;

            rot = new Quaternion(rot.x, rot.z, -rot.y, rot.w);

            Quaternion change = rot_calib * rot;
            this.transform.localRotation = camera_rot * change;
            //Debug.Log(camera_rot);
            //Debug.Log(Camera.main.transform.rotation);
            //Debug.Log(camera_rot * change );
            END */
        }
    }

	void UpdateViveEmitterCalibration()
	{
		//LEFT-handed (converted from RIGHT-handed when receiving data)
		TrackerTransform TrackerTableT = Instance.lastTrackerTransform_1;
		//Debug.Log (TrackerTableT.position);
		//Vive emitter transform according to tracker 1
		ViveEmitter.transform.rotation = ViveCASTTracker.transform.rotation * Quaternion.Inverse(TrackerTableT.rotation);
		ViveEmitter.transform.position = ViveCASTTracker.transform.position + ViveEmitter.transform.TransformVector (-TrackerTableT.position);
	}

	void UpdateViveHoloTracker()
	{
		//LEFT-handed (converted from RIGHT-handed when receiving data)
		TrackerTransform TrackerHoloT = Instance.lastTrackerTransform_0;
		ViveHoloTracker.transform.position = ViveEmitter.transform.position + ViveEmitter.transform.TransformVector (TrackerHoloT.position);
		ViveHoloTracker.transform.rotation = ViveEmitter.transform.rotation * TrackerHoloT.rotation;
	}

	void CalibrateViveWithHololens()
	{
		Vector3 HoloToTrackerDisplacement = new Vector3 (0, 0.08f, 0.04f);
		//LEFT-handed Unity reference system
		Transform HoloCameraT = Camera.main.transform;
		Transform ViveHoloTrackerT = ViveHoloTracker.transform;
		Vector3 ViveTrackerFromHoloPos = HoloCameraT.position + HoloCameraT.TransformVector (HoloToTrackerDisplacement);
		Vector3 ViveToHololensTranslation = ViveTrackerFromHoloPos - ViveHoloTrackerT.position;
		Quaternion ViveTrackersRotation = ViveHoloTracker.transform.rotation * Quaternion.Inverse(ViveCASTTracker.transform.rotation);;
		Quaternion ViveToHololensRotation = Quaternion.Euler(0, Quaternion.Inverse(ViveTrackersRotation * Quaternion.Inverse (HoloCameraT.rotation)).eulerAngles.y,0);

		//LEFT-handed (converted from RIGHT-handed when receiving data)
		TrackerTransform TrackerHoloT = Instance.lastTrackerTransform_0;
		//Vector3 EmitterFromVivePos = ViveHoloTracker.transform.position + ViveHoloTracker.transform.TransformVector (-TrackerHoloT.position);
		//Quaternion EmitterFromViveRot = ViveHoloTracker.transform.rotation * Quaternion.Inverse (TrackerHoloT.rotation);
		Vector3 OriginFromVivePos = ViveCASTTracker.transform.position - ViveHoloTracker.transform.position;
		Vector3 OriginFromHoloPos = ViveCASTTracker.transform.position - ViveTrackerFromHoloPos;
		Quaternion OriginFromViveRot = ViveCASTTracker.transform.rotation * Quaternion.Inverse (ViveHoloTracker.transform.rotation);
		//ViveCASTTrackerDebug.transform.position = ViveTrackerFromHoloPos +  Camera.main.transform.TransformVector (OriginFromVivePos);

		Quaternion FinalCASTOriginRot = ViveToHololensRotation;
		Vector3 FinalCASTOriginPos = ViveTrackerFromHoloPos + ViveToHololensRotation * (ViveCASTTracker.transform.position - ViveHoloTrackerT.position);
		ViveCASTTrackerDebug.transform.rotation = FinalCASTOriginRot;// HoloCameraT.rotation;// ViveToHololensRotation;
		ViveCASTTrackerDebug.transform.position = FinalCASTOriginPos;
			//ViveCASTTracker.transform.position + OriginFromVivePos - OriginFromHoloPos;
		
		CASTOrigin.transform.rotation = FinalCASTOriginRot;
		CASTOrigin.transform.position = FinalCASTOriginPos - ViveToHololensRotation * ViveCASTTracker.transform.position;
		//ViveCASTTrackerDebug.transform.rotation = ViveCASTTracker.transform.rotation * Camera.main.transform.rotation * Quaternion.Inverse(OriginFromViveRot);
	}

	public void CalibrateViveEmitter()
	{
		UpdateViveEmitterCalibration();
		ViveEmitter_calibrated = true;
	}

    public void StartCalibrateOrigin()
    {
        if (Instance.bTT_1)
        {
            Debug.Log("Start Table Calibration Procedure");
            calibrating = true;
            calib_confirmed = false;
            tableParent.transform.position = this.transform.position;
            tableParent.transform.rotation = this.transform.rotation;
            Vector3 tableCalib = new Vector3(0, -0.158f, 0);
            tableChild.transform.localPosition = new Vector3(0, 0, 0);
            tableChild.transform.localPosition = tableChild.transform.localPosition + tableCalib;
            GameObject.Find("Origin").transform.rotation = this.transform.rotation;
            GameObject.Find("Origin").transform.position = tableChild.transform.position;

            calibrating = false;
        }
        return;
    }

    public void CalibrateRotation()
    {
        if (Instance.bTT_0)
        {
            Debug.Log("Start Rotation Calibration Procedure");
            calibrating = true;
            Quaternion tracker_ori = Instance.lastTrackerTransform_0.rotation;
            camera_rot = Camera.main.transform.rotation;
            from0toTable = tracker_ori;
            // Quaternion re_rot = new Quaternion(0.666f, 0.230f, 0.230f, 0.671f);
            Quaternion re_rot = new Quaternion(from0toTable.x, from0toTable.z, -from0toTable.y, from0toTable.w);
            rot_calib = Quaternion.Inverse(re_rot);

            calibrating = false;
        }
        return;
    }
    public void StoreTableCalibration()
    {
        //set storedTable as child of ViveMeter object to stable local position and rotation of Table relative to ViveMeter;
        storedTable = new GameObject();
        storedTable.transform.position = GameObject.Find("Origin").transform.position;
        storedTable.transform.rotation = GameObject.Find("Origin").transform.rotation;
        storedTable.transform.parent = GameObject.Find("ViveMeter").transform;
        storedTablePos = storedTable.transform.localPosition;
        storedTableRot = storedTable.transform.localRotation;
        calib_confirmed = true;
    }
    //void LateUpdate()
    //{
    //    //Camera.main.transform.Rotate(new Vector3(0.01f, 0f, 0f));
    //    Debug.Log("LateUpdate");
    //    if (Instance.bTT)
    //    {
    //        //
    //        Debug.Log("Applying Tracker LateUpdate " + Instance.lastTrackerTransform.position.ToString());
    //        this.transform.position = Instance.lastTrackerTransform.position;
    //        this.transform.rotation = Instance.lastTrackerTransform.rotation;
    //        //Camera.main.transform.position = Instance.lastTrackerTransform.position;
    //        //Camera.main.transform.rotation = Instance.lastTrackerTransform.rotation;


    //    }
    //}

    class HoloClient : MonoBehaviour
    {
        private int port = 9001;
        private float output_rate = 0;
        //private TrackerTransform previousTrackerTransform = new TrackerTransform();
        private TrackerTransform _lastTrackerTransform_0 = new TrackerTransform();
        private TrackerTransform _lastTrackerTransform_1 = new TrackerTransform();

        public bool bTT_0 = false;
        public bool bTT_1 = false;
        public bool canReadCam_0 = false;
        public bool canReadCam_1 = false;
        private object _lock_0 = new object();
        private object _lock_1 = new object();

        public TrackerTransform lastTrackerTransform_0
        {
            get
            {
                lock (_lock_0)
                {
                    return _lastTrackerTransform_0;
                }
            }
            private set
            {
                lock (_lock_0)
                {
                    _lastTrackerTransform_0 = value;
                }
            }
        }
        public TrackerTransform lastTrackerTransform_1
        {
            get
            {
                lock (_lock_1)
                {
                    return _lastTrackerTransform_1;
                }
            }
            private set
            {
                lock (_lock_1)
                {
                    _lastTrackerTransform_1 = value;
                }
            }
        }

#if UNITY_WSA_10_0  && !UNITY_EDITOR 
        DatagramSocket socket;
        IOutputStream outstream;
        //DataReader reader;
        DataWriter writer;
#else
        UdpClient udp;
#endif

        IPEndPoint ep;

        float sps_time = 0;
        int count_sps = 0;

        public void Init(int port_)
        {
            Debug.Log("UDP Initialized");
            this.port = port_;
#if UNITY_WSA_10_0  && !UNITY_EDITOR
            socket = new DatagramSocket();
            socket.MessageReceived += SocketOnMessageReceived;
            socket.BindServiceNameAsync(port.ToString()).GetResults();
            //outstream = socket.GetOutputStreamAsync(new HostName(ep.Address.ToString()), port.ToString()).GetResults();
            //writer = new DataWriter(outstream);

#else
            udp = new UdpClient(port);
            udp.BeginReceive(new AsyncCallback(receiveMsg), null);
            Debug.Log("Begin Receive");
#endif
        }

        public void Init(int port_, float output_rate_)
        {
            this.output_rate = output_rate_;
            this.Init(port_);
        }

        // Use this for initialization
        void Start()
        {
            sps_time = 0;
            count_sps = 0;
        }


#if UNITY_WSA_10_0 && !UNITY_EDITOR

        //private async void SendMessage(string message)
        //{
        //    var socket = new DatagramSocket();

        //    using (var stream = await socket.GetOutputStreamAsync(new HostName(ep.Address.ToString()), port.ToString()))
        //    {
        //        using (var writer = new DataWriter(stream))
        //        {
        //            var data = Encoding.UTF8.GetBytes(message);

        //            writer.WriteBytes(data);
        //            writer.StoreAsync();
        //            //Debug.Log("sent " + data.Length);
        //        }
        //    }
        //}

        private async void SocketOnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            //Debug.Log("RECEIVED VOID");

            var result = args.GetDataStream();
            var resultStream = result.AsStreamForRead(1400);

            using (var reader = new StreamReader(resultStream))
            {
                var text = await reader.ReadToEndAsync();
                //var text = reader.ReadToEnd();

                handleMsg(text);
                //Debug.Log("MESSAGE: " + text);
            }
        }
#else

#endif

        // Update is called once per frame
        void Update()
        {
            //if (TransformChange == true)
            //{
            //    Vector3 positionChange = lastTrackerTransform.position - previousTrackerTransform.position;
            //    //Quaternion rotationChange = lastTrackerTransform.rotation - previousTrackerTransform.rotation;
            //    cube.transform.position = positionChange;
            //    cube.transform.rotation = lastTrackerTransform.rotation;
            //    TransformChange = false;
            //}
            if (canReadCam_0)
            {
                lastTrackerTransform_0.cam_position = Camera.main.transform.position;
                lastTrackerTransform_0.cam_rotation = Camera.main.transform.rotation;
                canReadCam_0 = false;
            }

            if (canReadCam_1)
            {
                lastTrackerTransform_1.cam_position = Camera.main.transform.position;
                lastTrackerTransform_1.cam_rotation = Camera.main.transform.rotation;
                canReadCam_1 = false;
            }

            if (output_rate > 0)
            {
                sps_time += Time.deltaTime;
                if (sps_time >= 1.0f / output_rate)
                {
                    //Debug.Log("Net SPS: " + count_sps / sps_time);// + " - " + output_rate);
                    sps_time = 0;
                    count_sps = 0;
                    //Debug.Log("TT " + lastTrackerTransform.id + " " + lastTrackerTransform.position + " Hr " + lastTrackerTransform.rotation.eulerAngles);
                    //Debug.Log("Rp " + lastHoloTransform.rposition + " Rr " + lastHoloTransform.rrotation.eulerAngles);
                    //Debug.Log("Ap " + lastHoloTransform.aposition + " Ar " + lastHoloTransform.arotation.eulerAngles);
                    //Debug.Log("Pp " + lastHoloTransform.pposition + " Pr " + lastHoloTransform.protation.eulerAngles);
                    //for(int i=0; i<lastHoloTransform.holoViewMatrices.Length; ++i)
                    //for (int i = 0; i < 2; ++i)
                    //{
                    //    //V Debug.Log("HVM_" + i + ": " + lastHoloMatrices.holoViewMatrices[i].ToString());
                    //    Debug.Log("HPM_" + i + ": " + lastHoloMatrices.holoProjMatrices[i].ToString());
                    //}
                    //Debug.Log("isStereo " + (lastHoloMatrices.isStereo ? "true":"false"));
                    //Debug.Log("Separation " + lastHoloMatrices.separation);
                    //Debug.Log("Convergence " + lastHoloMatrices.convergence);
                    //Debug.Log("Near " + lastHoloMatrices.near);
                    //Debug.Log("Far " + lastHoloMatrices.far);
                    //Debug.Log("Fov " + lastHoloMatrices.fov);
                    //Debug.Log("aspect " + lastHoloMatrices.aspect);

                }
            }

        }


        void receiveMsg(IAsyncResult result)
        {
            // while (!endReceive)
            {
                //Debug.Log("RECEIVING");

#if UNITY_WSA_10_0  && !UNITY_EDITOR

#else
                IPEndPoint source = new IPEndPoint(0, 0);
                //byte[] message = udp.EndReceive(result, ref source);
                //Debug.Log("RECV " + Encoding.UTF8.GetString(message) + " from " + source);

                string message = Encoding.UTF8.GetString(udp.EndReceive(result, ref source));
                //Debug.Log("RECV " + message + " from " + source);

                handleMsg(message);

                // schedule the next receive operation once reading is done:
                udp.BeginReceive(new AsyncCallback(receiveMsg), udp);

#endif
            }
        }

        void handleMsg(string message)
        {
            TrackerTransform tt = JsonUtility.FromJson<TrackerTransform>(message);
			//RIGHT-Handed coordinate system coming from vive
			//Converting to Unity's LEFT-handed ccords system
			tt.position.x *= -1.0f;
			tt.rotation = new Quaternion (-tt.rotation.x, tt.rotation.y, tt.rotation.z, -tt.rotation.w);

            if (tt.id.Equals("tracker0"))
            {
                bTT_0 = true;
                canReadCam_0 = true;
                lastTrackerTransform_0 = tt;
                //Debug.Log("HOLO " + ht.position.ToString() + " : " + ht.rotation.eulerAngles.ToString() + " from " + source);
                ++count_sps;
            }

            if (tt.id.Equals("tracker1"))
            {
                bTT_1 = true;
                canReadCam_1 = true;
                lastTrackerTransform_1 = tt;
                //Debug.Log("HOLO " + ht.position.ToString() + " : " + ht.rotation.eulerAngles.ToString() + " from " + source);

                ++count_sps;
            }
            //Debug.Log("HOLO " + tt.id + " " + tt.position.ToString() + " : " + tt.rotation.eulerAngles.ToString() + " from " + source);
            //++count_sps;
        }


        public void Stop()
        {
#if UNITY_WSA_10_0 && !UNITY_EDITOR

#else
            if (udp != null)
                udp.Close();
#endif
        }
    }
}

