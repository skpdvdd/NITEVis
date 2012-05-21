using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenNI;

namespace NITEVis
{
    public class Sensor : IDisposable
    {
        public event EventHandler GeneratorUpdate;
        public event EventHandler<NewUserEventArgs> NewUser;
        public event EventHandler<UserLostEventArgs> LostUser;

        readonly Context _context;
        readonly ScriptNode _scriptNode;
        readonly DepthGenerator _depthGenerator;
        readonly ImageGenerator _imageGenerator;
        readonly UserGenerator _userGenerator;
        readonly DepthMetaData _depthMetaData;
        readonly ImageMetaData _imageMetaData;

        readonly BitmapGenerator _bitmapGenerator;
        readonly int _imageWidth, _imageHeight;

        readonly Thread _readerThread;
        readonly AutoResetEvent _readerWaitHandle;

        bool _run, _pause;

        public int ImageWidth { get { return _imageWidth; } }
        public int ImageHeight { get { return _imageHeight; } }

        public Context Context { get { return _context; } }

        public DepthMetaData DepthMetaData { get { return _depthMetaData; } }
        public ImageMetaData ImageMetaData { get { return _imageMetaData; } }

        public DepthGenerator DepthGenerator { get { return _depthGenerator; } }
        public ImageGenerator ImageGenerator { get { return _imageGenerator; } }
        public UserGenerator UserGenerator { get { return _userGenerator; } }

        public BitmapSource DepthBitmap { get { return _bitmapGenerator.DepthBitmap; } }
        public BitmapSource LabelBitmap { get { return _bitmapGenerator.LabelBitmap; } }
        public BitmapSource RGBBitmap { get { return _bitmapGenerator.ImageBitmap; } }

        public Sensor(string config)
        {
            if (string.IsNullOrEmpty(config))
                throw new ArgumentNullException();

            try
            {
                _context = Context.CreateFromXmlFile(config, out _scriptNode);
                _depthGenerator = _context.FindExistingNode(NodeType.Depth) as DepthGenerator;
                _imageGenerator = _context.FindExistingNode(NodeType.Image) as ImageGenerator;
                _userGenerator = _context.FindExistingNode(NodeType.User) as UserGenerator;

                if (_depthGenerator == null)
                    throw new ApplicationException("No depth node found.");

                if (_imageGenerator == null)
                    throw new ApplicationException("No image node found.");

                if (_userGenerator == null)
                    throw new ApplicationException("No user node found.");

                if (_depthGenerator.MapOutputMode.FPS != _imageGenerator.MapOutputMode.FPS)
                    throw new ApplicationException("Depth and image node must have common framerates.");

                if (_depthGenerator.MapOutputMode.XRes != _imageGenerator.MapOutputMode.XRes)
                    throw new ApplicationException("Depth and image node must have common horizontal resolutions.");

                if (_depthGenerator.MapOutputMode.YRes != _imageGenerator.MapOutputMode.YRes)
                    throw new ApplicationException("Depth and image node must have common vertical resolutions.");

                _depthMetaData = new DepthMetaData();
                _imageMetaData = new ImageMetaData();

                _imageWidth = _depthGenerator.MapOutputMode.XRes;
                _imageHeight = _depthGenerator.MapOutputMode.YRes;

                _userGenerator.NewUser += new EventHandler<NewUserEventArgs>(_userGenerator_NewUser);
                _userGenerator.LostUser += new EventHandler<UserLostEventArgs>(_userGenerator_LostUser);
                _userGenerator.StartGenerating();

                _bitmapGenerator = new BitmapGenerator(this);

                _readerWaitHandle = new AutoResetEvent(false);

                _readerThread = new Thread(delegate()
                {
                    try
                    {
                        while (_run)
                        {
                            if (_pause)
                                _readerWaitHandle.WaitOne();

                            _context.WaitAndUpdateAll();

                            _depthGenerator.GetMetaData(_depthMetaData);
                            _imageGenerator.GetMetaData(_imageMetaData);

                            if (_depthMetaData.XRes != _imageWidth || _imageMetaData.XRes != _imageWidth)
                                throw new ApplicationException("Image width must not change.");

                            if (_depthMetaData.YRes != _imageHeight || _imageMetaData.YRes != _imageHeight)
                                throw new ApplicationException("Image height must not change.");

                            if (GeneratorUpdate != null)
                                GeneratorUpdate(this, EventArgs.Empty);
                        }
                    }
                    catch (ThreadInterruptedException)
                    {
                        Console.WriteLine("Reader thread interrupted.");
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException("Error while processing sensor data.", e);
                    }
                }) { Name = "ONI Reader Thread" };
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Start()
        {
            _run = true;
            _readerThread.Start();
        }

        public void Pause(bool pause)
        {
            _pause = pause;

            if (!_pause)
                _readerWaitHandle.Set();
        }

        public void Dispose()
        {
            _run = false;

            if (_readerThread != null && _readerThread.IsAlive)
            {
                _readerThread.Interrupt();
                _readerThread.Join();
            }
        }

        void _userGenerator_LostUser(object sender, UserLostEventArgs e)
        {
            if (LostUser != null)
                LostUser(this, e);
        }

        void _userGenerator_NewUser(object sender, NewUserEventArgs e)
        {
            string tname = Thread.CurrentThread.Name;

            if (NewUser != null)
                NewUser(this, e);
        }

        class BitmapGenerator
        {
            readonly object _lock;
            readonly Sensor _sensor;
            readonly byte[] _depthData, _labelData;
            readonly WriteableBitmap _depth, _label, _rgb;

            bool _depthValid, _labelValid, _rgbValid;

            public WriteableBitmap ImageBitmap
            {
                get
                {
                    lock (_lock)
                    {
                        if (!_rgbValid)
                        {
                            _rgb.Lock();
                            _rgb.WritePixels(new Int32Rect(0, 0, _sensor.ImageWidth, _sensor.ImageHeight), _sensor.ImageMetaData.ImageMapPtr, _sensor.ImageMetaData.DataSize, _rgb.BackBufferStride);
                            _rgb.Unlock();

                            _rgbValid = true;
                        }

                        return _rgb;
                    }
                }
            }

            public unsafe WriteableBitmap DepthBitmap
            {
                get
                {
                    lock (_lock)
                    {
                        if (!_depthValid)
                        {
                            int i = 0;
                            int width = _sensor.ImageWidth;
                            int height = _sensor.ImageHeight;
                            ushort* pDepth = (ushort*)_sensor.DepthMetaData.DepthMapPtr.ToPointer();

                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    ushort depth = *pDepth;
                                    _depthData[i] = (byte)(depth / 39.2157f);

                                    i++;
                                    pDepth++;
                                }
                            }

                            _depth.Lock();
                            _depth.WritePixels(new Int32Rect(0, 0, width, height), _depthData, _depth.BackBufferStride, 0);
                            _depth.Unlock();
                        }

                        _depthValid = true;
                    }

                    return _depth;
                }
            }

            public unsafe WriteableBitmap LabelBitmap
            {
                get
                {
                    lock (_lock)
                    {
                        if (!_labelValid)
                        {
                            int i = 0;
                            int width = _sensor.ImageWidth;
                            int height = _sensor.ImageHeight;
                            ushort* pLabel = (ushort*)_sensor.UserGenerator.GetUserPixels(0).LabelMapPtr.ToPointer();

                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    ushort label = *pLabel;
                                    _labelData[i] = (byte)label;

                                    i++;
                                    pLabel++;
                                }
                            }

                            _label.Lock();
                            _label.WritePixels(new Int32Rect(0, 0, width, height), _labelData, _label.BackBufferStride, 0);
                            _label.Unlock();

                            _labelValid = true;
                        }

                        return _label;
                    }
                }
            }

            public BitmapGenerator(Sensor sensor)
            {
                if (sensor == null)
                    throw new ArgumentNullException();

                _lock = new object();
                _sensor = sensor;

                _depthData = new byte[sensor.ImageWidth * sensor.ImageHeight];
                _labelData = new byte[sensor.ImageWidth * sensor.ImageHeight];

                _depth = new WriteableBitmap(sensor.ImageWidth, sensor.ImageHeight, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
                _label = new WriteableBitmap(sensor.ImageWidth, sensor.ImageHeight, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
                _rgb = new WriteableBitmap(sensor.ImageWidth, sensor.ImageHeight, 96, 96, System.Windows.Media.PixelFormats.Rgb24, null);

                sensor.GeneratorUpdate += new EventHandler(delegate(object sender, EventArgs e)
                {
                    lock (_lock)
                    {
                        _depthValid = false;
                        _labelValid = false;
                        _rgbValid = false;
                    }
                });
            }
        }
    }
}
