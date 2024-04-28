// Copyright (c) 2010-2022 Lockheed Martin Corporation. All rights reserved.
// Use of this file is bound by the PREPAR3D® SOFTWARE DEVELOPER KIT END USER LICENSE AGREEMENT

//
// Managed Data Request sample
//
// Click on Connect to try and connect to a running version of Prepar3D
// Click on Request Data any number of times
// Click on Disconnect to close the connection, and then you should
// be able to click on Connect and restart the process
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

// Gaby -- imports to use LiveCharts
using LiveCharts;
//using LiveCharts.Wpf;
using LiveCharts.WinForms;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
//using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.Defaults;
//using LiveChartsCore.SkiaSharpView.WinForms;

// Ryan-- Livecharts2
/*
using System.Linq;
//using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
*/

// Add these two statements to all SimConnect clients
using LockheedMartin.Prepar3D.SimConnect;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Managed_Dashboard;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using LiveChartsCore.SkiaSharpView.WinForms;
using OpenTK.Graphics.OpenGL;
using LiveChartsCore.SkiaSharpView.SKCharts;

namespace Managed_Dashboard
{
    public partial class Form1 : Form
    {

        // Gaby --
        private LiveCharts.WinForms.CartesianChart altitude_chart;
        private LiveCharts.WinForms.CartesianChart speed_chart;
        private LiveCharts.WinForms.CartesianChart pb_chart;
        // Add a Panel control to the form
        private Panel chartPanel;
        // Add a GroupBox to contain the chart
        private GroupBox altitudeGroupBox;
        private GroupBox speedGroupBox;
        private GroupBox pbGroupBox;


        private Label timeTextBox;
        private Label latitudeTextBox;
        private Label longitudeTextBox;
        private Label timeLabel;
        private Label latitudeLabel;
        private Label longitudeLabel;

        private PolarChart magneticHeadingChart;
        private GroupBox magneticHeadingGroupBox;
        private ObservableValue headingValue = new ObservableValue(0);

        // Ryan--
        private double prev_time = 0;

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        SimConnect simconnect = null;

        // Ryan-- timer will space out requests by 1 second
        Timer requestTimer = new Timer();
        enum DEFINITIONS
        {
            Struct1,
        }

        enum DATA_REQUESTS
        {
            REQUEST_1,
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct Struct1
        {
            // this is how you declare a fixed size string
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String title;
            public double latitude;
            public double longitude;
            public double altitude;
            // Ryan-- adding speed property
            public double speed;
            // Ryan-- absolute time
            public double time;
            // Ryan--
            public double magnetic_heading;
            // Ryan--
            public double pitch;
            public double bank;
        };

        private void InitializeTextBoxes()
        {
            // Initialize Labels
            timeLabel = new Label
            {
                Text = "Time:",
                Location = new Point(400, 12),
                Size = new Size(100, 20)
            };
            Controls.Add(timeLabel);

            timeTextBox = new Label
            {
                Location = new Point(400, 30),
                Size = new Size(150, 20)
                //ReadOnly = true
            };
            Controls.Add(timeTextBox);

            // Latitude
            latitudeLabel = new Label
            {
                Text = "Latitude:",
                Location = new Point(620, 12),
                Size = new Size(100, 20)
            };
            Controls.Add(latitudeLabel);

            latitudeTextBox = new Label
            {
                Location = new Point(620, 30),
                Size = new Size(150, 20)
                //ReadOnly = true
            };
            Controls.Add(latitudeTextBox);

            // Longitude
            longitudeLabel = new Label
            {
                Text = "Longitude:",
                Location = new Point(790, 12),
                Size = new Size(100, 20)
            };
            Controls.Add(longitudeLabel);

            longitudeTextBox = new Label
            {
                Location = new Point(790, 30),
                Size = new Size(150, 20)
                //ReadOnly = true
            };
            Controls.Add(longitudeTextBox);
        }

        public Form1()
        {

            InitializeComponent();
            InitializeTextBoxes();

            chartPanel = new Panel()
            {
                Dock = DockStyle.Fill // Fill the entire form area
            };
            Controls.Add(chartPanel);
            // Initialize altitude and speed GroupBoxes
            InitializeAllGroupBox();

            // Ryan-- remove middle button parameter
            setButtons(true, false);
            
            // Ryan-- Set timer interval to 1 second
            requestTimer.Interval = 1000;
            requestTimer.Tick += RequestTimer_Tick;

            // Initialize the chart
            InitializeAltitude();
            InitializeSpeed();
            Initializepb();
            InitializeHeading();
            altitudeGroupBox.Controls.Add(altitude_chart);
            speedGroupBox.Controls.Add(speed_chart);
            pbGroupBox.Controls.Add(pb_chart);
            magneticHeadingGroupBox.Controls.Add(magneticHeadingChart);

        }
        // Simconnect client will send a win32 message when there is 
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.

        protected override void DefWndProc(ref Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT)
            {
                if (simconnect != null)
                {
                    simconnect.ReceiveMessage();
                }
            }
            else
            {
                base.DefWndProc(ref m);
            }
        }

        // Ryan-- remove one parameter (bool bGet). commented out middle button line.
        private void setButtons(bool bConnect, bool bDisconnect)
        {
            buttonConnect.Enabled = bConnect;
            // buttonRequestData.Enabled = bGet;
            buttonDisconnect.Enabled = bDisconnect;
        }

        private void closeConnection()
        {
            if (simconnect != null)
            {
                // Dispose serves the same purpose as SimConnect_Close()
                simconnect.Dispose();
                simconnect = null;
                // Ryan--
                Debug.WriteLine("Connection closed");
            }
        }

        // Set up all the SimConnect related data definitions and event handlers
        private void initDataRequest()
        {
            try
            {
                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- changed from altitude to altitude above ground
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Alt Above Ground", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- on data request, we also define speed
                // Gaby -- fixed to be able to update speed
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED); // Add speed definition
                // Ryan-- get absolute time from epoch in seconds
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Absolute Time", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- get the x and y velocity
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Heading Degrees Magnetic", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Pitch Degrees", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Bank Degrees", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                
                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<Struct1>(DEFINITIONS.Struct1);

                // catch a simobject data request
                simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simconnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                // Ryan--
                Debug.WriteLine(ex.Message);
            }
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            // Ryan--
            Debug.WriteLine("Connected to Prepar3D");

            // Ryan-- Start the timer when connected
            requestTimer.Start();
        }

        // The case where the user closes Prepar3D
        void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            // Ryan--
            Debug.WriteLine("Prepar3D has exited");
            closeConnection();
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            // Ryan--
            Debug.WriteLine("Exception received: " + data.dwException);
        }



        // The case where the user closes the client
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closeConnection();
        }

        void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {

            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    Struct1 s1 = (Struct1)data.dwData[0];

                    TimeSpan ts = TimeSpan.FromSeconds(s1.time);// Ryan--

                    DateTime dateTime = new DateTime(1, 1, 1, 0, 0, 0) + ts;

                    // Ryan--
                    // Convert radians to degrees
                    double heading_degrees = s1.magnetic_heading * (180 / Math.PI);
                    //   UpdateMagneticHeading(heading_degrees);
                    double pitch_degrees = s1.pitch * (180 / Math.PI);
                    double bank_degrees = s1.bank * (180 / Math.PI);

                    // Ryan--
                    // Only update charts if the time has updated.
                    // Time will not update if simulation is paused.
                    if (prev_time != s1.time)
                    {
                        // Ryan-- print to debug console instead of text box.
                        Debug.WriteLine("Title: " + s1.title);
                        Debug.WriteLine("Lat:   " + s1.latitude);
                        Debug.WriteLine("Lon:   " + s1.longitude);
                        Debug.WriteLine("Alt:   " + s1.altitude);
                        // Ryan-- adding ground speed to display
                        Debug.WriteLine("Speed: " + s1.speed);
                        // Ryan-- display time
                        Debug.WriteLine("Time: " + dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        Debug.WriteLine("Time: " + s1.time);
                        // Ryan-- magnetic heading
                        Debug.WriteLine("Magnetic heading: " + s1.magnetic_heading);
                        Debug.WriteLine("Magnetic heading: " + heading_degrees);
                        // Ryan-- current pitch, bank, heading
                        Debug.WriteLine("Pitch: " + s1.pitch);
                        Debug.WriteLine("Bank: " + s1.bank);

                        // Send info to ChartForm
                        // Gaby
                        altitude_chart.Series[0].Values.Add(s1.altitude);
                        speed_chart.Series[0].Values.Add(s1.speed);
                        pb_chart.Series[0].Values.Add(s1.pitch);
                        pb_chart.Series[1].Values.Add(s1.bank);
                        // Update text boxes with new data
                        timeTextBox.Text = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        latitudeTextBox.Text = s1.latitude.ToString("F6");
                        longitudeTextBox.Text = s1.longitude.ToString("F6");
                        prev_time = s1.time;
                    }
                    break;

                default:
                    Debug.WriteLine("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }

        private void InitializeAllGroupBox()
        {
            // altitude group box
            altitudeGroupBox = new GroupBox()
            {
                Text = "Altitude Chart",
                Location = new System.Drawing.Point(10, 70),
                Size = new System.Drawing.Size(500, 350),

            };

            chartPanel.Controls.Add(altitudeGroupBox);

            // speed group box
            speedGroupBox = new GroupBox()
            {
                Text = "Speed Chart",
                Location = new System.Drawing.Point(10, 430),
                Size = new System.Drawing.Size(500, 350),
            };

            chartPanel.Controls.Add(speedGroupBox);

            // pitch, bank heading group box
            pbGroupBox = new GroupBox()
            {
                Text = "Pitch, Bank",
                Location = new System.Drawing.Point(520, 430),
                Size = new System.Drawing.Size(500, 350),

            };

            chartPanel.Controls.Add(pbGroupBox);

            //heading group box
            magneticHeadingGroupBox = new GroupBox
            {
                Text = "Magnetic Heading",
                Location = new Point(520, 70),
                Size = new Size(500, 350),
            };
            chartPanel.Controls.Add(magneticHeadingGroupBox);
        }

        private void InitializeAltitude()
        {
            // Create a new Cartesian chart
            altitude_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };
            
            // Define X axis
            altitude_chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time (seconds)", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            altitude_chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Altitude (feet)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var altitudeSeries = new LineSeries
            {
                Title = "Altitude",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle, // Set shape for series 1 points
            };
            altitude_chart.Zoom = ZoomingOptions.X;
            altitude_chart.Pan = PanningOptions.X;
            // Add the series to the chart
            altitude_chart.Series.Add(altitudeSeries);
        }

        private void InitializeSpeed()
        {
            // Create a new Cartesian chart
            speed_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            speed_chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time (seconds)", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            speed_chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Speed (knots)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var speedSeries = new LineSeries
            {
                Title = "Speed",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle, // Hide points on the line
            };
            speed_chart.Zoom = ZoomingOptions.X;
            speed_chart.Pan = PanningOptions.X;
            // Add the series to the chart
            speed_chart.Series.Add(speedSeries);
        }

        private void Initializepb()
        {
            // Create a new Cartesian chart
            pb_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            pb_chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time (seconds)", // X axis label
                LabelFormatter = value => value.ToString("N3"), // Optional formatting for axis labels
            });

            // Define Y axis
            pb_chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Radians", // Y axis label
                LabelFormatter = value => value.ToString("N3"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var pitchSeries = new LineSeries
            {
                Title = "Pitch",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle // Hide points on the line
            };

            var bankSeries = new LineSeries
            {
                Title = "Bank",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle // Hide points on the line
            };

            // Add the series to the chart
            pb_chart.LegendLocation = LegendLocation.Bottom; // Change this to control legend location
            pb_chart.Zoom = ZoomingOptions.X;
            pb_chart.Pan = PanningOptions.X;

            pb_chart.Series.Add(pitchSeries);
            pb_chart.Series.Add(bankSeries);
            //pb_chart.Series.Add(headingSeries);
        }
        private void InitializeHeading()
        {
            Debug.WriteLine("Initializing Magnetic Heading Chart...");
            /*
            var headingSeries = new PolarLineSeries<ObservablePolarPoint>
            {
                Values = new ObservableCollection<ObservablePolarPoint>
                {
                    new ObservablePolarPoint { Angle = 0, Radius = 10 },
                    new ObservablePolarPoint { Angle = 45, Radius = 15 },
                    new ObservablePolarPoint { Angle = 90, Radius = 20 },
                    // Continue adding points as necessary
                },
                IsClosed = false,
                Fill = null,
            };
            */
            magneticHeadingChart = new PolarChart();
            magneticHeadingChart.Series = new[]
            {
                new PolarLineSeries<double>
                {
                    Values = new double[] {15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1},
                    Fill = null,
                    IsClosed = false,
                }
            };
            //magneticHeadingChart.Size = new System.Drawing.Size(400, 400);
            // magneticHeadingChart.Location = new System.Drawing.Point(550, 80);/
            /*
            {
                Series = new ISeries[] { headingSeries },
                AngleAxes = new PolarAxis[]
                {
                    new PolarAxis
                    {
                        MinLimit = 0,
                        MaxLimit = 360,
                        Labeler = angle => $"{angle}°",
                    }
                },
                Location = new Point(520, 70),
                Size = new Size(500, 350),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };*/
            Debug.WriteLine($"Chart Location: {magneticHeadingChart.Location}, Size: {magneticHeadingChart.Size}");
            Debug.WriteLine($"Group Box Location: {magneticHeadingGroupBox.Location}, Size: {magneticHeadingGroupBox.Size}");

            magneticHeadingGroupBox.Controls.Add(magneticHeadingChart);
            Debug.WriteLine("Number of Controls in Magnetic Heading Group Box: " + magneticHeadingGroupBox.Controls.Count);
        }

        /*
        private void UpdateMagneticHeading(double headingDegrees)
        {
            headingValue.Value = headingDegrees;
        }
        */
        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (simconnect == null)
            {
                try
                {
                    // the constructor is similar to SimConnect_Open in the native API4
                    // Ryan-- change name to Managed Dashboard
                    simconnect = new SimConnect("Managed Dashboard", this.Handle, WM_USER_SIMCONNECT, null, 0);
                   
                    // Ryan-- middle parameter removed
                    setButtons(false, true);

                    initDataRequest();
                }
                catch (COMException ex)
                {
                    // Ryan--
                    Debug.WriteLine("Unable to connect to Prepar3D:\n\n" + ex.Message);
                }
            }
            else
            {
                // Ryan--
                Debug.WriteLine("Error - try again");
                closeConnection();

                // Ryan-- middle parameter removed
                setButtons(true, false);
            }
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            // clear all the current values from the graphs
            altitude_chart.Series[0].Values.Clear();
            speed_chart.Series[0].Values.Clear();
            pb_chart.Series[0].Values.Clear();
            pb_chart.Series[1].Values.Clear();

            closeConnection();
            // Ryan-- middle parameter removed
            setButtons(true, false);
        }

        // Ryan-- new request event handler
        private void RequestTimer_Tick(object sender, EventArgs e)
        {
            // Send data request every second
            if (simconnect != null)
            {
                simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                // Ryan--
                Debug.WriteLine("Request sent...");
            }
        }
    
    }
}
