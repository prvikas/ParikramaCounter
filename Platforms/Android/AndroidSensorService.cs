using Android.Content;
using Android.Hardware;
using Android.Runtime;
using System;
using ParikramaCounter.Services;
using AndroidApp = Android.App;

namespace ParikramaCounter.Platforms.Android
{
    public class AndroidSensorService : Java.Lang.Object, ISensorEventListener, ISensorService
    {
        private SensorManager sensorManager;
        private Sensor accelerometer, gyroscope, magnetometer;

        private readonly object sensorLock = new object();
        private float[] accelValues = new float[3];
        private float[] gyroValues  = new float[3];
        private float[] magValues   = new float[3];
        private bool hasAccel  = false;
        private bool hasMag    = false;
        private bool isRunning = false;
        private bool currentHighRate = false;

        public int HardwareStepCount { get; private set; }
        public void UpdateStepCount(int count) => HardwareStepCount = count;

        public event Action<double[], double[], double[]> SensorDataReceived;

        public AndroidSensorService()
        {
            sensorManager = (SensorManager)AndroidApp.Application.Context.GetSystemService(Context.SensorService);
            accelerometer = sensorManager.GetDefaultSensor(SensorType.Accelerometer);
            gyroscope     = sensorManager.GetDefaultSensor(SensorType.Gyroscope);
            magnetometer  = sensorManager.GetDefaultSensor(SensorType.MagneticField);
        }

        public void Start(bool highRate = false)
        {
            if (isRunning) return;
            isRunning       = true;
            currentHighRate = highRate;
            var delay = highRate ? SensorDelay.Game : SensorDelay.Ui;
            Register(delay);
        }

        public void SetRate(bool highRate)
        {
            if (!isRunning || currentHighRate == highRate) return;
            currentHighRate = highRate;
            sensorManager.UnregisterListener(this);
            lock (sensorLock) { hasAccel = false; hasMag = false; }
            Register(highRate ? SensorDelay.Game : SensorDelay.Ui);
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            sensorManager.UnregisterListener(this);
            lock (sensorLock) { hasAccel = false; hasMag = false; }
        }

        private void Register(SensorDelay delay)
        {
            if (accelerometer != null) sensorManager.RegisterListener(this, accelerometer, delay);
            if (gyroscope     != null) sensorManager.RegisterListener(this, gyroscope,     delay);
            if (magnetometer  != null) sensorManager.RegisterListener(this, magnetometer,  delay);
        }

        public void OnSensorChanged(SensorEvent e)
        {
            double[] accel, gyro, mag;
            lock (sensorLock)
            {
                if (e.Sensor.Type == SensorType.Accelerometer)
                {
                    accelValues[0] = e.Values[0]; accelValues[1] = e.Values[1]; accelValues[2] = e.Values[2];
                    hasAccel = true;
                }
                else if (e.Sensor.Type == SensorType.Gyroscope)
                {
                    gyroValues[0] = e.Values[0]; gyroValues[1] = e.Values[1]; gyroValues[2] = e.Values[2];
                }
                else if (e.Sensor.Type == SensorType.MagneticField)
                {
                    magValues[0] = e.Values[0]; magValues[1] = e.Values[1]; magValues[2] = e.Values[2];
                    hasMag = true;
                }
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
