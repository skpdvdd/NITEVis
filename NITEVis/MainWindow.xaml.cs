using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenNI;

namespace NITEVis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool _paused;
        Sensor _sensor;
        ImageBrush _labelDataBrush;
        SkeletonDrawer _skeletonDrawer;
        SkeletonCapability _skeletonCapability;
        Dictionary<int, Dictionary<SkeletonJoint, SkeletonJointPosition>> _skeleton;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.PauseInitially)
            {
                _paused = true;
                _sensor.Pause(true);
            }

            _sensor.Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if(_sensor != null)
                _sensor.Dispose();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Pause)
            {
                _paused = !_paused;
                _sensor.Pause(_paused);
            } 
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            string configPath = Properties.Settings.Default.ONIConfig;

            Title += " - " + configPath;

            try
            {
                if (!File.Exists(configPath))
                    throw new ApplicationException("Config file '" + configPath + "' does not exist.");

                _labelDataBrush = Resources["labelData"] as ImageBrush;

                _skeletonDrawer = new SkeletonDrawer(depthGrid);
                _skeleton = new Dictionary<int, Dictionary<SkeletonJoint, SkeletonJointPosition>>();

                _sensor = new Sensor(configPath);

                Console.WriteLine("Sensor initialized successfully.");

                _skeletonCapability = _sensor.UserGenerator.SkeletonCapability;
                _skeletonCapability.SetSkeletonProfile(SkeletonProfile.All);
                _skeletonCapability.SetSmoothing(Properties.Settings.Default.SkeletonSmoothing);

                _sensor.GeneratorUpdate += delegate(object s, EventArgs args)
                {
                    Action action = () =>
                    {
                        _labelDataBrush.ImageSource = _sensor.LabelBitmap;

                        depthImage.Source = _sensor.DepthBitmap;
                        rgbImage.Source = _sensor.RGBBitmap;

                        if (Properties.Settings.Default.DisplaySkeleton && _skeleton.Count > 0)
                        {
                            var users = _skeleton.Keys;

                            Func<int, SkeletonJoint, SkeletonJointPosition> jointPosition = (int user, SkeletonJoint joint) =>
                            {
                                SkeletonJointPosition position = _skeletonCapability.GetSkeletonJointPosition(user, joint);

                                if (position.Position.Z == 0)
                                    position.Confidence = 0;
                                else
                                    position.Position = _sensor.DepthGenerator.ConvertRealWorldToProjective(position.Position);

                                return position;
                            };

                            foreach (int user in users)
                            {
                                _skeleton[user][SkeletonJoint.Head] = jointPosition(user, SkeletonJoint.Head);
                                _skeleton[user][SkeletonJoint.Neck] = jointPosition(user, SkeletonJoint.Neck);
                                _skeleton[user][SkeletonJoint.Torso] = jointPosition(user, SkeletonJoint.Torso);

                                _skeleton[user][SkeletonJoint.LeftShoulder] = jointPosition(user, SkeletonJoint.LeftShoulder);
                                _skeleton[user][SkeletonJoint.LeftElbow] = jointPosition(user, SkeletonJoint.LeftElbow);
                                _skeleton[user][SkeletonJoint.LeftHand] = jointPosition(user, SkeletonJoint.LeftHand);

                                _skeleton[user][SkeletonJoint.RightShoulder] = jointPosition(user, SkeletonJoint.RightShoulder);
                                _skeleton[user][SkeletonJoint.RightElbow] = jointPosition(user, SkeletonJoint.RightElbow);
                                _skeleton[user][SkeletonJoint.RightHand] = jointPosition(user, SkeletonJoint.RightHand);

                                _skeleton[user][SkeletonJoint.LeftHip] = jointPosition(user, SkeletonJoint.LeftHip);
                                _skeleton[user][SkeletonJoint.LeftKnee] = jointPosition(user, SkeletonJoint.LeftKnee);
                                _skeleton[user][SkeletonJoint.LeftFoot] = jointPosition(user, SkeletonJoint.LeftFoot);

                                _skeleton[user][SkeletonJoint.RightHip] = jointPosition(user, SkeletonJoint.RightHip);
                                _skeleton[user][SkeletonJoint.RightKnee] = jointPosition(user, SkeletonJoint.RightKnee);
                                _skeleton[user][SkeletonJoint.RightFoot] = jointPosition(user, SkeletonJoint.RightFoot);
                            }

                            _skeletonDrawer.Draw(_skeleton);
                        }
                    };

                    Dispatcher.Invoke(action);
                };

                _sensor.NewUser += delegate(object s, NewUserEventArgs args)
                {
                    Console.WriteLine("[" + args.ID + "] Found");

                    if(Properties.Settings.Default.DisplaySkeleton)
                        _skeletonCapability.RequestCalibration(args.ID, true);
                };

                _sensor.LostUser += delegate(object s, UserLostEventArgs args)
                {
                    Console.WriteLine("[" + args.ID + "] Lost");

                    _skeleton.Remove(args.ID);

                    Action action = () =>
                    {
                        _skeletonDrawer.UserLost(args.ID);
                    };

                    Dispatcher.Invoke(action);
                };

                _skeletonCapability.CalibrationComplete += delegate(object s, CalibrationProgressEventArgs args)
                {
                    Console.WriteLine("[" + args.ID + "] Calibration status: " + args.Status);

                    if (args.Status == CalibrationStatus.OK)
                    {
                        _skeletonCapability.StartTracking(args.ID);

                        Dictionary<SkeletonJoint, SkeletonJointPosition> jointPositions = new Dictionary<SkeletonJoint, SkeletonJointPosition>();

                        jointPositions.Add(SkeletonJoint.Head, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.Neck, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.Torso, new SkeletonJointPosition());

                        jointPositions.Add(SkeletonJoint.LeftShoulder, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.LeftElbow, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.LeftHand, new SkeletonJointPosition());

                        jointPositions.Add(SkeletonJoint.RightShoulder, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.RightElbow, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.RightHand, new SkeletonJointPosition());

                        jointPositions.Add(SkeletonJoint.LeftHip, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.LeftKnee, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.LeftFoot, new SkeletonJointPosition());

                        jointPositions.Add(SkeletonJoint.RightHip, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.RightKnee, new SkeletonJointPosition());
                        jointPositions.Add(SkeletonJoint.RightFoot, new SkeletonJointPosition());

                        _skeleton.Add(args.ID, jointPositions);
                    }
                    else if (args.Status != CalibrationStatus.ManualAbort)
                        _skeletonCapability.RequestCalibration(args.ID, true);
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                MessageBox.Show(ex.Message, "Error while initializing OpenNI", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        class SkeletonDrawer
        {
            readonly Panel _panel;
            readonly Brush _lineBrush, _lowConfidenceLineBrush;
            readonly double _strokeWeight;
            readonly Dictionary<int, Line[]> _lines;

            public SkeletonDrawer(Panel lineCollectionBase)
            {
                var color = Properties.Settings.Default.SkeletonColor;
                var lowConfidenceColor = Properties.Settings.Default.SkeletonLowConfidenceColor;

                _panel = lineCollectionBase;
                _strokeWeight = Properties.Settings.Default.SkeletonThickness;
                _lineBrush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
                _lowConfidenceLineBrush = new SolidColorBrush(Color.FromRgb(lowConfidenceColor.R, lowConfidenceColor.G, lowConfidenceColor.B));
                _lines = new Dictionary<int, Line[]>();
            }

            public void Draw(Dictionary<int, Dictionary<SkeletonJoint, SkeletonJointPosition>> data)
            {
                foreach (var skeleton in data)
                {
                    if (!_lines.ContainsKey(skeleton.Key))
                    {
                        _lines.Add(skeleton.Key, new Line[16]);

                        for (int i = 0; i < 16; i++)
                        {
                            _lines[skeleton.Key][i] = new Line() { Stroke = _lineBrush, StrokeThickness = _strokeWeight };
                            _panel.Children.Add(_lines[skeleton.Key][i]);
                        }
                    }

                    Line[] lines = _lines[skeleton.Key];
                    double tconf = Math.Min(1, Math.Max(0, Properties.Settings.Default.SkeletonLowConfidenceThreshold));
                    Func<SkeletonJoint, Point3D> jointPosition = (SkeletonJoint joint) => skeleton.Value[joint].Position;

                    lines[0].X1 = jointPosition(SkeletonJoint.Head).X;
                    lines[0].Y1 = jointPosition(SkeletonJoint.Head).Y;
                    lines[0].X2 = jointPosition(SkeletonJoint.Neck).X;
                    lines[0].Y2 = jointPosition(SkeletonJoint.Neck).Y;
                    lines[0].Stroke = (skeleton.Value[SkeletonJoint.Head].Confidence > tconf && skeleton.Value[SkeletonJoint.Neck].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[1].X1 = jointPosition(SkeletonJoint.Neck).X;
                    lines[1].Y1 = jointPosition(SkeletonJoint.Neck).Y;
                    lines[1].X2 = jointPosition(SkeletonJoint.RightShoulder).X;
                    lines[1].Y2 = jointPosition(SkeletonJoint.RightShoulder).Y;
                    lines[1].Stroke = (skeleton.Value[SkeletonJoint.Neck].Confidence > tconf && skeleton.Value[SkeletonJoint.RightShoulder].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[2].X1 = jointPosition(SkeletonJoint.Neck).X;
                    lines[2].Y1 = jointPosition(SkeletonJoint.Neck).Y;
                    lines[2].X2 = jointPosition(SkeletonJoint.LeftShoulder).X;
                    lines[2].Y2 = jointPosition(SkeletonJoint.LeftShoulder).Y;
                    lines[2].Stroke = (skeleton.Value[SkeletonJoint.Neck].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftShoulder].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[3].X1 = jointPosition(SkeletonJoint.RightShoulder).X;
                    lines[3].Y1 = jointPosition(SkeletonJoint.RightShoulder).Y;
                    lines[3].X2 = jointPosition(SkeletonJoint.RightElbow).X;
                    lines[3].Y2 = jointPosition(SkeletonJoint.RightElbow).Y;
                    lines[3].Stroke = (skeleton.Value[SkeletonJoint.RightShoulder].Confidence > tconf && skeleton.Value[SkeletonJoint.RightElbow].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[4].X1 = jointPosition(SkeletonJoint.LeftShoulder).X;
                    lines[4].Y1 = jointPosition(SkeletonJoint.LeftShoulder).Y;
                    lines[4].X2 = jointPosition(SkeletonJoint.LeftElbow).X;
                    lines[4].Y2 = jointPosition(SkeletonJoint.LeftElbow).Y;
                    lines[4].Stroke = (skeleton.Value[SkeletonJoint.LeftShoulder].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftElbow].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[5].X1 = jointPosition(SkeletonJoint.RightElbow).X;
                    lines[5].Y1 = jointPosition(SkeletonJoint.RightElbow).Y;
                    lines[5].X2 = jointPosition(SkeletonJoint.RightHand).X;
                    lines[5].Y2 = jointPosition(SkeletonJoint.RightHand).Y;
                    lines[5].Stroke = (skeleton.Value[SkeletonJoint.RightElbow].Confidence > tconf && skeleton.Value[SkeletonJoint.RightHand].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[6].X1 = jointPosition(SkeletonJoint.LeftElbow).X;
                    lines[6].Y1 = jointPosition(SkeletonJoint.LeftElbow).Y;
                    lines[6].X2 = jointPosition(SkeletonJoint.LeftHand).X;
                    lines[6].Y2 = jointPosition(SkeletonJoint.LeftHand).Y;
                    lines[6].Stroke = (skeleton.Value[SkeletonJoint.LeftElbow].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftHand].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[7].X1 = jointPosition(SkeletonJoint.RightShoulder).X;
                    lines[7].Y1 = jointPosition(SkeletonJoint.RightShoulder).Y;
                    lines[7].X2 = jointPosition(SkeletonJoint.Torso).X;
                    lines[7].Y2 = jointPosition(SkeletonJoint.Torso).Y;
                    lines[7].Stroke = (skeleton.Value[SkeletonJoint.RightShoulder].Confidence > tconf && skeleton.Value[SkeletonJoint.Torso].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[8].X1 = jointPosition(SkeletonJoint.LeftShoulder).X;
                    lines[8].Y1 = jointPosition(SkeletonJoint.LeftShoulder).Y;
                    lines[8].X2 = jointPosition(SkeletonJoint.Torso).X;
                    lines[8].Y2 = jointPosition(SkeletonJoint.Torso).Y;
                    lines[8].Stroke = (skeleton.Value[SkeletonJoint.LeftShoulder].Confidence > tconf && skeleton.Value[SkeletonJoint.Torso].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[9].X1 = jointPosition(SkeletonJoint.Torso).X;
                    lines[9].Y1 = jointPosition(SkeletonJoint.Torso).Y;
                    lines[9].X2 = jointPosition(SkeletonJoint.RightHip).X;
                    lines[9].Y2 = jointPosition(SkeletonJoint.RightHip).Y;
                    lines[9].Stroke = (skeleton.Value[SkeletonJoint.Torso].Confidence > tconf && skeleton.Value[SkeletonJoint.RightHip].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[10].X1 = jointPosition(SkeletonJoint.Torso).X;
                    lines[10].Y1 = jointPosition(SkeletonJoint.Torso).Y;
                    lines[10].X2 = jointPosition(SkeletonJoint.LeftHip).X;
                    lines[10].Y2 = jointPosition(SkeletonJoint.LeftHip).Y;
                    lines[10].Stroke = (skeleton.Value[SkeletonJoint.Torso].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftHip].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[11].X1 = jointPosition(SkeletonJoint.RightHip).X;
                    lines[11].Y1 = jointPosition(SkeletonJoint.RightHip).Y;
                    lines[11].X2 = jointPosition(SkeletonJoint.LeftHip).X;
                    lines[11].Y2 = jointPosition(SkeletonJoint.LeftHip).Y;
                    lines[11].Stroke = (skeleton.Value[SkeletonJoint.RightHip].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftHip].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[12].X1 = jointPosition(SkeletonJoint.RightHip).X;
                    lines[12].Y1 = jointPosition(SkeletonJoint.RightHip).Y;
                    lines[12].X2 = jointPosition(SkeletonJoint.RightKnee).X;
                    lines[12].Y2 = jointPosition(SkeletonJoint.RightKnee).Y;
                    lines[12].Stroke = (skeleton.Value[SkeletonJoint.RightHip].Confidence > tconf && skeleton.Value[SkeletonJoint.RightKnee].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[13].X1 = jointPosition(SkeletonJoint.LeftHip).X;
                    lines[13].Y1 = jointPosition(SkeletonJoint.LeftHip).Y;
                    lines[13].X2 = jointPosition(SkeletonJoint.LeftKnee).X;
                    lines[13].Y2 = jointPosition(SkeletonJoint.LeftKnee).Y;
                    lines[13].Stroke = (skeleton.Value[SkeletonJoint.LeftHip].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftKnee].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[14].X1 = jointPosition(SkeletonJoint.RightKnee).X;
                    lines[14].Y1 = jointPosition(SkeletonJoint.RightKnee).Y;
                    lines[14].X2 = jointPosition(SkeletonJoint.RightFoot).X;
                    lines[14].Y2 = jointPosition(SkeletonJoint.RightFoot).Y;
                    lines[14].Stroke = (skeleton.Value[SkeletonJoint.RightKnee].Confidence > tconf && skeleton.Value[SkeletonJoint.RightFoot].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;

                    lines[15].X1 = jointPosition(SkeletonJoint.LeftKnee).X;
                    lines[15].Y1 = jointPosition(SkeletonJoint.LeftKnee).Y;
                    lines[15].X2 = jointPosition(SkeletonJoint.LeftFoot).X;
                    lines[15].Y2 = jointPosition(SkeletonJoint.LeftFoot).Y;
                    lines[15].Stroke = (skeleton.Value[SkeletonJoint.LeftKnee].Confidence > tconf && skeleton.Value[SkeletonJoint.LeftFoot].Confidence > tconf) ? _lineBrush : _lowConfidenceLineBrush;
                }
            }

            public void UserLost(int user)
            {
                if(!_lines.ContainsKey(user))
                    return;

                foreach(Line line in _lines[user])
                    _panel.Children.Remove(line);

                _lines.Remove(user);
            }
        }
    }
}
