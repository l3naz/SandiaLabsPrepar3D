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

namespace Managed_Dashboard
{
    public partial class Form1 : Form
    {

        // Gaby --
        private LiveCharts.WinForms.CartesianChart altitude_chart;
        private LiveCharts.WinForms.CartesianChart speed_chart;
        // Add a Panel control to the form
        private Panel chartPanel;
        // Add a GroupBox to contain the chart
        private GroupBox altitudeGroupBox;
        private GroupBox speedGroupBox;

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
        };

        public Form1()
        {

            InitializeComponent();

            chartPanel = new Panel()
            {
                Dock = DockStyle.Fill // Fill the entire form area
            };
            Controls.Add(chartPanel);
            // Initialize altitude and speed GroupBoxes
            InitializeAltitudeGroupBox();
            InitializeSpeedGroupBox();

            // Ryan-- remove middle button parameter
            setButtons(true, false);
            
            // Ryan-- Set timer interval to 1 second
            requestTimer.Interval = 1000;
            requestTimer.Tick += RequestTimer_Tick;

            // Initialize the chart
            InitializeAltitude();
            InitializeSpeed();
            altitudeGroupBox.Controls.Add(altitude_chart);
            speedGroupBox.Controls.Add(speed_chart);

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
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- changed from altitude to altitude above ground
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Alt Above Ground", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- on data request, we also define speed
                // Gaby -- fixed to be able to update speed
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED); // Add speed definition
                // Ryan-- get absolute time from epoch in seconds
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Absolute Time", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- get the x and y velocity
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Heading Degrees True", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
       
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

                    // Ryan--
                    // Convert seconds to ticks (1 tick = 100 nanoseconds)
                    // Create DateTime object from ticks
                    long ticks = (long)(s1.time * TimeSpan.TicksPerSecond);
                    DateTime dateTime = new DateTime(ticks, DateTimeKind.Utc);

                    // Ryan--
                    // Convert radians to degrees
                    double degrees_north = s1.magnetic_heading * (180 / Math.PI);

                    // Ryan--
                    // Only update charts if the time has updated.
                    // Time will not update if simulation is paused.
                    if (prev_time != s1.time)
                    {
                        // Ryan--
                        Debug.WriteLine("Title: " + s1.title);
                        Debug.WriteLine("Lat:   " + s1.latitude);
                        Debug.WriteLine("Lon:   " + s1.longitude);
                        Debug.WriteLine("Alt:   " + s1.altitude);
                        // Ryan-- adding ground speed to display
                        Debug.WriteLine("Speed: " + s1.speed);
                        // Ryan-- display time
                        Debug.WriteLine("Time: " + dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        // Ryan--
                        Debug.WriteLine("Magnetic heading: " + degrees_north);

                        // Send info to ChartForm
                        // Gaby
                        altitude_chart.Series[0].Values.Add(s1.altitude);
                        speed_chart.Series[0].Values.Add(s1.speed);
                        prev_time = s1.time;
                    }
                    break;

                default:
                    Debug.WriteLine("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }

        private void InitializeAltitudeGroupBox()
        {
            altitudeGroupBox = new GroupBox()
            {
                Text = "Altitude Chart",
                Location = new System.Drawing.Point(10, 70),
                Size = new System.Drawing.Size(500, 350),

            };

            chartPanel.Controls.Add(altitudeGroupBox);
        }

        private void InitializeSpeedGroupBox()
        {
            speedGroupBox = new GroupBox()
            {
                Text = "Speed Chart",
                Location = new System.Drawing.Point(10, 430),
                Size = new System.Drawing.Size(500, 350),
            };

            chartPanel.Controls.Add(speedGroupBox);
        }

        private void InitializeAltitude()
        {
            // Create a new Cartesian chart
            altitude_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            altitude_chart.AxisX.Add(new Axis
            {
                Title = "Time (seconds)", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            altitude_chart.AxisY.Add(new Axis
            {
                Title = "Altitude (feet)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var altitudeSeries = new LineSeries
            {
                Title = "Altitude",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = null // Hide points on the line
            };

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
            speed_chart.AxisX.Add(new Axis
            {
                Title = "Time (seconds)", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            speed_chart.AxisY.Add(new Axis
            {
                Title = "Speed (knots)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var speedSeries = new LineSeries
            {
                Title = "Speed",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = null // Hide points on the line
            };

            // Add the series to the chart
            speed_chart.Series.Add(speedSeries);
        }
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
