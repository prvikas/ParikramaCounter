using Android.Content;
using Android.Hardware;
using Android.Runtime;
using System;
using System.Threading;
using ParikramaCounter.Services;
using AndroidApp = Android.App;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(ParikramaCounter.Platforms.Android.AndroidSensorService))]

namespace ParikramaCounter.Platforms.Android
{
    public class AndroidSensorService : Java.Lang.Object, ISensorEventListener, ISensorService
    {
        private SensorManager sensorManager;
        private Sensor accelerometer, gyroscope, magnetometer;

        // Fix #3: lock guards shared arrays from concurrent sensor callbacks
        private readonly object sensorLock = new object();
        private float[] accelValues = new float[3];
        private float[] gyroValues = new float[3];
        private float[] magValues = new float[3];

        // Fix #1: track which sensors have fired at least once so we don't
        // dispatch with stale zero-arrays for sensors that haven't updated yet
        // hasGyro removed — gyro is optional and the gate only needs accel+mag
        private bool hasAccel = false;
        private bool hasMag = false;
        private bool isRunning = false;

        public event Action<double[], double[], double[]> SensorDataReceived;

        public AndroidSensorService()
        {
            sensorManager = (SensorManager)AndroidApp.Application.Context.GetSystemService(Context.SensorService);
            accelerometer = sensorManager.GetDefaultSensor(SensorType.Accelerometer);
            gyroscope = sensorManager.GetDefaultSensor(SensorType.Gyroscope);
            magnetometer = sensorManager.GetDefaultSensor(SensorType.MagneticField);
        }

        public void Start()
        {
            // Fix #1: guard against double-registration which doubles callback rate on Android
            if (isRunning) return;
            isRunning = true;

            if (accelerometer != null)
                sensorManager.RegisterListener(this, accelerometer, SensorDelay.Game);
            if (gyroscope != null)
                sensorManager.RegisterListener(this, gyroscope, SensorDelay.Game);
            if (magnetometer != null)
                sensorManager.RegisterListener(this, magnetometer, SensorDelay.Game);
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;

            sensorManager.UnregisterListener(this);
            lock (sensorLock)
            {
                hasAccel = false;
                hasMag = false;
            }
        }

        public void OnSensorChanged(SensorEvent e)
        {
            double[] accel, gyro, mag;

            lock (sensorLock)
            {
                if (e.Sensor.Type == SensorType.Accelerometer)
                {
                    accelValues[0] = e.Values[0];
                    accelValues[1] = e.Values[1];
                    accelValues[2] = e.Values[2];
                    hasAccel = true;
                }
                else if (e.Sensor.Type == SensorType.Gyroscope)
                {
                    gyroValues[0] = e.Values[0];
                    gyroValues[1] = e.Values[1];
                    gyroValues[2] = e.Values[2];
                }
                else if (e.Sensor.Type == SensorType.MagneticField)
                {
                    magValues[0] = e.Values[0];
                    magValues[1] = e.Values[1];
                    magValues[2] = e.Values[2];
                    hasMag = true;
                }

                // Only dispatch once accel and mag have both provided real data
                if (!hasAccel || !hasMag) return;

                accel = new double[] { accelValues[0], accelValues[1], accelValues[2] };
                gyro  = new double[] { gyroValues[0],  gyroValues[1],  gyroValues[2]  };
                mag   = new double[] { magValues[0],   magValues[1],   magValues[2]   };
            }

            SensorDataReceived?.Invoke(accel, gyro, mag);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy) { }
    }
}
