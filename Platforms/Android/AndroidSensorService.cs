using Android.Content;
using Android.Hardware;
using Android.Runtime;
using System;
using System.Linq;
using ParikramaCounter.Services;
using AndroidApp = Android.App;

// Make sure this is OUTSIDE the namespace and uses the correct type
[assembly: Microsoft.Maui.Controls.Dependency(typeof(ParikramaCounter.Platforms.Android.AndroidSensorService))]

namespace ParikramaCounter.Platforms.Android
{
    public class AndroidSensorService : Java.Lang.Object, ISensorEventListener, ISensorService
    {
        private SensorManager? sensorManager;
        private Sensor? accelerometer, gyroscope, magnetometer;

        private float[] accelValues = new float[3];
        private float[] gyroValues = new float[3];
        private float[] magValues = new float[3];

        public event Action<double[], double[], double[]>? SensorDataReceived;

        public AndroidSensorService()
        {
            sensorManager = AndroidApp.Application.Context.GetSystemService(Context.SensorService) as SensorManager;
            accelerometer = sensorManager?.GetDefaultSensor(SensorType.Accelerometer);
            gyroscope = sensorManager?.GetDefaultSensor(SensorType.Gyroscope);
            magnetometer = sensorManager?.GetDefaultSensor(SensorType.MagneticField);
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
            if (sensorManager != null)
                sensorManager.UnregisterListener(this);
        }

        public void OnSensorChanged(SensorEvent? e)
        {
            if (e == null || e.Sensor == null || e.Values == null)
                return;

            if (e.Sensor.Type == SensorType.Accelerometer)
                accelValues = e.Values.ToArray();
            else if (e.Sensor.Type == SensorType.Gyroscope)
                gyroValues = e.Values.ToArray();
            else if (e.Sensor.Type == SensorType.MagneticField)
                magValues = e.Values.ToArray();

            double[] accel = Array.ConvertAll(accelValues, x => (double)x);
            double[] gyro = Array.ConvertAll(gyroValues, x => (double)x);
            double[] mag = Array.ConvertAll(magValues, x => (double)x);

            SensorDataReceived?.Invoke(accel, gyro, mag);
        }

        public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) { }
    }
}
