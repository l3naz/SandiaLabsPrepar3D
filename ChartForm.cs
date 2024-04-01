using System;
using System.Windows.Forms;
using LiveCharts;
using LiveCharts.WinForms;
using LiveCharts.Wpf;

namespace Managed_Data_Request
{
    public partial class ChartForm : Form
    {
        public ChartForm()
        {
            InitializeComponent();

            // Initialize the chart
            InitializeChart();
        }

        private void InitializeChart()
        {
            // Create a new Cartesian chart
            var chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Altitude (feet)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Add the chart to the form
            Controls.Add(chart);

            // Define a new LineSeries for altitude data
            var altitudeSeries = new LineSeries
            {
                Title = "Altitude",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = null // Hide points on the line
            };

            // Add the series to the chart
            chart.Series.Add(altitudeSeries);
        }

        // Method to update altitude data in the chart
        public void UpdateAltitude(double altitude)
        {
            // Get the chart from the form controls
            var chart = Controls[0] as LiveCharts.WinForms.CartesianChart;

            // Get the altitude series from the chart
            var altitudeSeries = chart.Series[0] as LineSeries;

            // Add the new altitude value to the series
            altitudeSeries.Values.Add(altitude);

            // Limit the number of displayed data points (optional)
            if (altitudeSeries.Values.Count > 50)
            {
                altitudeSeries.Values.RemoveAt(0);
            }

            // Refresh the chart
            chart.Update();
        }
    }
}
