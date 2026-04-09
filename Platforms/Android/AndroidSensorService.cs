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
        private bool hasAccel = false;
        private bool hasGyro = false;
        private bool hasMag = false;

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
            if (accelerometer != null)
                sensorManager.RegisterListener(this, accelerometer, SensorDelay.Game);
            if (gyroscope != null)
                sensorManager.RegisterListener(this, gyroscope, SensorDelay.Game);
            if (magnetometer != null)
                sensorManager.RegisterListener(this, magnetometer, SensorDelay.Game);
        }

        public void Stop()
        {
            sensorManager.UnregisterListener(this);
            // Reset ready flags so a subsequent Start() waits for fresh data
            lock (sensorLock)
            {
                hasAccel = false;
                hasGyro = false;
                hasMag = false;
            }
        }

        public void OnSensorChanged(SensorEvent e)
        {
            // Fix #1 & #3: update the correct array under the lock, then only
            // dispatch once all three sensors have provided at least one reading.
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
                    hasGyro = true;
                }
                else if (e.Sensor.Type == SensorType.MagneticField)
                {
                    magValues[0] = e.Values[0];
                    magValues[1] = e.Values[1];
                    magValues[2] = e.Values[2];
                    hasMag = true;
                }

                if (!hasAccel || !hasMag) return; // gyro optional, accel+mag required

                accel = new double[] { accelValues[0], accelValues[1], accelValues[2] };
                gyro  = new double[] { gyroValues[0],  gyroValues[1],  gyroValues[2]  };
                mag   = new double[] { magValues[0],   magValues[1],   magValues[2]   };
            }

            SensorDataReceived?.Invoke(accel, gyro, mag);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy) { }
    }
}
