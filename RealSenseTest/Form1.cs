using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using nuitrack;
using nuitrack.issues;
using Newtonsoft.Json;
using Exception = System.Exception;
using System.Threading;
using System.Text;
using System.Data;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.IO;

namespace RealSenseTest
{
    public partial class MainForm : Form
    {

       // private Graphics gph;
        //----------------------------
        static MqttClient mqttClient;
        static Socket socket;
        //----------------------------

        private DirectBitmap _bitmap;
        private bool _visualizeColorImage = true;
        private bool _colorStreamEnabled = false;

        private DepthSensor _depthSensor;
        private ColorSensor _colorSensor;
        private UserTracker _userTracker;
        private SkeletonTracker _skeletonTracker;
        private GestureRecognizer _gestureRecognizer;
        private HandTracker _handTracker;

        private DepthFrame _depthFrame;
        private SkeletonData _skeletonData;
        private HandTrackerData _handTrackerData;
        private IssuesData _issuesData = null;
        
        //----------------------------------------------------------
        static void Awake()
        {
            try
            {
                //連結伺服器
                mqttClient = new MqttClient("163.18.62.45", 1883, false, null, null, null);
                //註冊伺服器返回資訊接受函數
                //mqttClient.MqttMsgPublishReceived += new MqttClient.MqttMsgPublishEventHandler(messageReceive);
                //用戶端ID
                string clientId = Guid.NewGuid().ToString();
                mqttClient.Connect(clientId);

                //訂閱
                mqttClient.Subscribe(new string[] { "/F435/3" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            }
            catch
            {
                Console.WriteLine("MQTT連線失敗");
            }

            //Socket client TCP
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint ipEnd = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 55021);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(ipEnd);
            }
            catch (SocketException e)
            {
                Console.Write("Fail to connect server");
                Console.Write(e.ToString());
                return;
            }

        }
        /* static void messageReceive(object sender, MqttMsgPublishEventArgs e)
         {
             string msg = "Topic:" + e.Topic + "   Message:" + e.Message[0];

             Console.WriteLine(msg);
             Console.WriteLine(System.Text.Encoding.Default.GetString(e.Message) + "\n");
         }*/




        //------------------------------------------------------------



    public MainForm()
        {
            InitializeComponent();


            // Initialize Nuitrack. This should be called before using any Nuitrack module.
            // By passing the default arguments we specify that Nuitrack must determine
            // the location automatically.

            Awake();



            try
            {
                Nuitrack.Init("");
            }
            catch (Exception exception)
            {
                Console.WriteLine("Cannot initialize Nuitrack.");
                throw exception;
            }

            try
            {
                // Create and setup all required modules
                _depthSensor = DepthSensor.Create();
                _colorSensor = ColorSensor.Create();
                _userTracker = UserTracker.Create();
                _skeletonTracker = SkeletonTracker.Create();
                _handTracker = HandTracker.Create();
                _gestureRecognizer = GestureRecognizer.Create();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Cannot create Nuitrack module.");
                throw exception;
            }

            _depthSensor.SetMirror(false);

            // Add event handlers for all modules
            _depthSensor.OnUpdateEvent += onDepthSensorUpdate;
            _colorSensor.OnUpdateEvent += onColorSensorUpdate;
            _userTracker.OnUpdateEvent += onUserTrackerUpdate;
            _skeletonTracker.OnSkeletonUpdateEvent += onSkeletonUpdate;
            _handTracker.OnUpdateEvent += onHandTrackerUpdate;
            _gestureRecognizer.OnNewGesturesEvent += onNewGestures;

            // Add an event handler for the IssueUpdate event 
            Nuitrack.onIssueUpdateEvent += onIssueDataUpdate;

            // Create and configure the Bitmap object according to the depth sensor output mode
            OutputMode mode = _depthSensor.GetOutputMode();
            OutputMode colorMode = _colorSensor.GetOutputMode();

            if (mode.XRes < colorMode.XRes)
                mode.XRes = colorMode.XRes;
            if (mode.YRes < colorMode.YRes)
                mode.YRes = colorMode.YRes;

            _bitmap = new DirectBitmap(mode.XRes, mode.YRes);
            for (int y = 0; y < mode.YRes; ++y)
            {
                for (int x = 0; x < mode.XRes; ++x)
                    _bitmap.SetPixel(x, y, Color.FromKnownColor(KnownColor.Aqua));
            }

            // Set fixed form size
            this.MinimumSize = this.MaximumSize = new Size(mode.XRes, mode.YRes);

            // Disable unnecessary caption bar buttons
            this.MinimizeBox = this.MaximizeBox = false;
        
            // Enable double buffering to prevent flicker
            this.DoubleBuffered = true;

            // Run Nuitrack. This starts sensor data processing.
            try
            {
                Nuitrack.Run();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Cannot start Nuitrack.");
                throw exception;
            }
            

            this.Show();
            
        }
        //-----------------------------------------------------------
        /*void loop()
        {
            while (true)
            {
                mqttClient.Publish("/F435/2", Encoding.UTF8.GetBytes("your data"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                Thread.Sleep(1500);
            }
        }
       */
        //-----------------------------------------------------------
        ~MainForm()
        {
            _bitmap.Dispose();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Release Nuitrack and remove all modules
            try
            {
                Nuitrack.onIssueUpdateEvent -= onIssueDataUpdate;

                _depthSensor.OnUpdateEvent -= onDepthSensorUpdate;
                _colorSensor.OnUpdateEvent -= onColorSensorUpdate;
                _userTracker.OnUpdateEvent -= onUserTrackerUpdate;
                _skeletonTracker.OnSkeletonUpdateEvent -= onSkeletonUpdate;
                _handTracker.OnUpdateEvent -= onHandTrackerUpdate;
                _gestureRecognizer.OnNewGesturesEvent -= onNewGestures;

                Nuitrack.Release();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Nuitrack release failed.");
                throw exception;
            }
        }

        // Switch visualization mode on a mouse click
        protected override void OnClick(EventArgs args)
        {
           // base.OnClick(args);

           // _visualizeColorImage = !_visualizeColorImage;
        }

        
        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);

            // Update Nuitrack data. Data will be synchronized with skeleton time stamps.
            try
            {
                Nuitrack.Update(_skeletonTracker);
            }
            catch (LicenseNotAcquiredException exception)
            {
                Console.WriteLine("LicenseNotAcquired exception. Exception: ", exception);
                throw exception;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Nuitrack update failed. Exception: ", exception);
            }

            // Draw a bitmap
            args.Graphics.DrawImage(_bitmap.Bitmap, new Point(0, 0));

            
            // Draw skeleton joints
            if (_skeletonData != null)
            {
                const int jointSize = 10;
                foreach (var skeleton in _skeletonData.Skeletons)
                {
                    SolidBrush brush = new SolidBrush(Color.FromArgb(255 - 40 * skeleton.ID, 0, 0));
                    SolidBrush brushShoulder = new SolidBrush(Color.Black); //nick 2019-8-26
                    Pen GreenPen = new Pen(Color.Green, 5);
                    String JosnBuffer = "";
                    bool firstList = true;
                    foreach (var joint in skeleton.Joints)
                    {
                        
                        //-----------------------------------------------------------------------------------------------nick 2019-8-26
                        if (firstList)
                        {
                            firstList = false;
                            JosnBuffer += "{\"" + joint.Type.ToString() + "\":{ \"x\":" + joint.Proj.X.ToString() + ",\"y\":" + joint.Proj.Y.ToString() + ",\"z\":" + joint.Proj.Z.ToString() + "}";
                        }
                        else
                            JosnBuffer += ",\"" + joint.Type.ToString() + "\":{ \"x\":" + joint.Proj.X.ToString() + ",\"y\":" + joint.Proj.Y.ToString() + ",\"z\":" + joint.Proj.Z.ToString() + "}";

                        //-------------------------------------------------------------------------------------------------------------
                        //args.Graphics.FillEllipse(brush, joint.Proj.X * _bitmap.Width - jointSize / 2,
                                          //   joint.Proj.Y * _bitmap.Height - jointSize / 2, jointSize, jointSize);
                        //args.Graphics.DrawLine(brushShoulder, joint.Proj.X * _bitmap.Width - jointSize / 2,
                          //joint.Proj.Y * _bitmap.Height - jointSize / 2, jointSize, jointSize);
                        
                    
                    }
                    JosnBuffer += "}";
                   /* for (int i = 1; i< 4; i++)//頭到腰的骨架
                    {
                     
                        int z = 1;
                        z=z+i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[i].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[i].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[z].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[z].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }
                    for (int i = 5; i < 9; i++)//左上半身
                    {

                        int z = 1;
                        z = z + i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[i].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[i].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[z].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[z].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }
                    for (int i = 11; i < 15; i++)//右上半身
                    {

                        int z = 1;
                        z = z + i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[i].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[i].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[z].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[z].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }
                    for (int i = 11; i < 15; i++)//腰到右臀
                    {

                        int z = 1;
                        z = z + i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[4].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[4].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[17].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[17].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }
                    for (int i = 11; i < 15; i++)//腰到右臀
                    {

                        int z = 1;
                        z = z + i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[4].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[4].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[21].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[21].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }
                    for (int i = 17; i < 19; i++)//腰到右臀
                    {

                        int z = 1;
                        z = z + i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[i].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[i].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[z].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[z].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }
                    for (int i = 21; i < 23; i++)//腰到右臀
                    {

                        int z = 1;
                        z = z + i;
                        args.Graphics.DrawLine(GreenPen, skeleton.Joints[i].Proj.X * _bitmap.Width - jointSize / 2,
                          skeleton.Joints[i].Proj.Y * _bitmap.Height - jointSize / 2, skeleton.Joints[z].Proj.X * _bitmap.Width - jointSize / 2, skeleton.Joints[z].Proj.Y * _bitmap.Height - jointSize / 2);
                        Console.WriteLine(z);
                    }*/
                    Console.WriteLine(skeleton.Joints[13].Proj.Y);
                    Console.WriteLine(skeleton.Joints[14].Proj.Y);
                    //Console.WriteLine(_bitmap.Width.ToString()+ "," +_bitmap.Height.ToString());
                    socket.Send(Encoding.UTF8.GetBytes(JosnBuffer), JosnBuffer.Length, 0);
                    //Thread.Sleep(10);
                    //mqttClient.Publish("/F435/3", Encoding.UTF8.GetBytes(JosnBuffer), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                }
            }

            /*
            DataSet dataSet = new DataSet("dataSet"); //建立DataSet
            DataTable table = new DataTable(); //建立DataTable
            DataColumn LshColumn = new DataColumn("Lsh", typeof(float)); //建立id欄
            DataColumn type1Column = new DataColumn("shoulder"); //建立部位欄
                                                            //DataTable加入欄位
            table.Columns.Add(LshColumn);
            table.Columns.Add(type1Column);

            dataSet.Tables.Add(table);

                DataRow newRow = table.NewRow();
                newRow["id"] = Lsh;
                //newRow["item"] = "這是第" +  + "個項目";
                table.Rows.Add(newRow);


            string json = JsonConvert.SerializeObject(dataSet);
           */


            //mqttClient.Publish("/F435/3", Encoding.UTF8.GetBytes(""), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            Thread.Sleep(1);

            // Draw hand pointers
            /* if (_handTrackerData != null)
             {
                 foreach (var userHands in _handTrackerData.UsersHands)
                 {
                     if (userHands.LeftHand != null)
                     {
                         HandContent hand = userHands.LeftHand.Value;
                         int size = hand.Click ? 20 : 30;
                         Brush brush = new SolidBrush(Color.Aquamarine);
                         args.Graphics.FillEllipse(brush, hand.X * _bitmap.Width - size / 2, hand.Y * _bitmap.Height - size / 2, size, size);
                     }

                     if (userHands.RightHand != null)
                     {
                         HandContent hand = userHands.RightHand.Value;
                         int size = hand.Click ? 20 : 30;
                         Brush brush = new SolidBrush(Color.DarkBlue);
                         args.Graphics.FillEllipse(brush, hand.X * _bitmap.Width - size / 2, hand.Y * _bitmap.Height - size / 2, size, size);
                     }
                 }
             }*/

            // Update Form
            this.Invalidate();
        }

        private void onIssueDataUpdate(IssuesData issuesData)
        {
            _issuesData = issuesData;
        }

        // Event handler for the DepthSensorUpdate event
        private void onDepthSensorUpdate(DepthFrame depthFrame)
        {
            _depthFrame = depthFrame;
        }

        // Event handler for the ColorSensorUpdate event
        private void onColorSensorUpdate(ColorFrame colorFrame)
        {
            if (!_visualizeColorImage)
                return;

            _colorStreamEnabled = true;

            float wStep = (float)_bitmap.Width / colorFrame.Cols;
            float hStep = (float)_bitmap.Height / colorFrame.Rows;

            float nextVerticalBorder = hStep;

            Byte[] data = colorFrame.Data;
            int colorPtr = 0;
            int bitmapPtr = 0;
            const int elemSizeInBytes = 3;

            for (int i = 0; i < _bitmap.Height; ++i)
            {
                if (i == (int)nextVerticalBorder)
                {
                    colorPtr += colorFrame.Cols * elemSizeInBytes;
                    nextVerticalBorder += hStep;
                }

                int offset = 0;
                int argb = data[colorPtr]
                    | (data[colorPtr + 1] << 8)
                    | (data[colorPtr + 2] << 16)
                    | (0xFF << 24);
                float nextHorizontalBorder = wStep;
                for (int j = 0; j < _bitmap.Width; ++j)
                {
                    if (j == (int)nextHorizontalBorder)
                    {
                        offset += elemSizeInBytes;
                        argb = data[colorPtr + offset]
                            | (data[colorPtr + offset + 1] << 8)
                            | (data[colorPtr + offset + 2] << 16)
                            | (0xFF << 24);
                        nextHorizontalBorder += wStep;
                    }

                    _bitmap.Bits[bitmapPtr++] = argb;
                }
            }
        }

        // Event handler for the UserTrackerUpdate event
        private void onUserTrackerUpdate(UserFrame userFrame)
        {
            if (_visualizeColorImage && _colorStreamEnabled)
                return;
            if (_depthFrame == null)
                return;

            const int MAX_LABELS = 7;
            bool[] labelIssueState = new bool[MAX_LABELS];
            for (UInt16 label = 0; label < MAX_LABELS; ++label)
            {
                labelIssueState[label] = false;
                if (_issuesData != null)
                {
                    FrameBorderIssue frameBorderIssue = _issuesData.GetUserIssue<FrameBorderIssue>(label);
                    labelIssueState[label] = (frameBorderIssue != null);
                }
            }

            float wStep = (float)_bitmap.Width / _depthFrame.Cols;
            float hStep = (float)_bitmap.Height / _depthFrame.Rows;

            float nextVerticalBorder = hStep;

            Byte[] dataDepth = _depthFrame.Data;
            Byte[] dataUser = userFrame.Data;
            int dataPtr = 0;
            int bitmapPtr = 0;
            const int elemSizeInBytes = 2;
            for (int i = 0; i < _bitmap.Height; ++i)
            {
                if (i == (int)nextVerticalBorder)
                {
                    dataPtr += _depthFrame.Cols * elemSizeInBytes;
                    nextVerticalBorder += hStep;
                }

                int offset = 0;
                int argb = 0;
                int label = dataUser[dataPtr] | dataUser[dataPtr + 1] << 8;
                int depth = Math.Min(255, (dataDepth[dataPtr] | dataDepth[dataPtr + 1] << 8) / 32);
                float nextHorizontalBorder = wStep;
                for (int j = 0; j < _bitmap.Width; ++j)
                {
                    if (j == (int)nextHorizontalBorder)
                    {
                        offset += elemSizeInBytes;
                        label = dataUser[dataPtr + offset] | dataUser[dataPtr + offset + 1] << 8;
                        if (label == 0)
                            depth = Math.Min(255, (dataDepth[dataPtr + offset] | dataDepth[dataPtr + offset + 1] << 8) / 32);
                        nextHorizontalBorder += wStep;
                    }

                    if (label > 0)
                    {
                        int user = label * 40;
                        if (!labelIssueState[1])
                            user += 40;
                        argb = 0 | (user << 8) | (0 << 16) | (0xFF << 24);
                    }
                    else
                    {
                        argb = depth | (depth << 8) | (depth << 16) | (0xFF << 24);
                    }

                    _bitmap.Bits[bitmapPtr++] = argb;
                }
            }

        }

        // Event handler for the SkeletonUpdate event
        private void onSkeletonUpdate(SkeletonData skeletonData)
        {
            _skeletonData = skeletonData;
        }

        // Event handler for the HandTrackerUpdate event
        private void onHandTrackerUpdate(HandTrackerData handTrackerData)
        {
            _handTrackerData = handTrackerData;
        }

        // Event handler for the gesture detection event
        private void onNewGestures(GestureData gestureData)
        {
            // Display the information about detected gestures in the console
            foreach (var gesture in gestureData.Gestures)
                Console.WriteLine("Recognized {0} from user {1}", gesture.Type.ToString(), gesture.UserID);
        }

       
    }

    public class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; set; }

        public Int32[] Bits { get; set; }
        public bool Disposed { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }

        protected GCHandle BitsHandle { get; set; }

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, Color colour)
        {
            int index = x + (y * Width);
            int col = colour.ToArgb();

            Bits[index] = col;
        }

        public Color GetPixel(int x, int y)
        {
            int index = x + (y * Width);
            int col = Bits[index];
            Color result = Color.FromArgb(col);

            return result;
        }

        public void Dispose()
        {
            if (Disposed)
                return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();

        }
    }

}
