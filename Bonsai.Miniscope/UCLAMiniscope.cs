using System;
using OpenCV.Net;
using System.Reactive.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing.Design;

namespace Bonsai.Miniscope
{
    [Description("Produces a video sequence of images acquired from a UCLA Miniscope.")]
    public class UCLAMiniscope : Source<IplImage>
    {
        // Settings
        [Description("The index of the camera from which to acquire images.")]
        public int Index { get; set; } = 0;

        [Range(1, 255)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        [Description("Relative exposure time.")]
        public double Exposure { get; set; } = 255;

        [Range(1, 2)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        [Description("The sensor gain.")]
        public double SensorGain { get; set; } = 2;

        private double lastExposure;
        private double lastSensorGain;

        // State
        IObservable<IplImage> source;
        readonly object captureLock = new object();
        //readonly CapturePropertyCollection captureProperties = new CapturePropertyCollection();

        // Functor
        public UCLAMiniscope()
        {
            lastExposure = Exposure;
            lastSensorGain = SensorGain;

            source = Observable.Create<IplImage>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    lock (captureLock)
                    {
                        using (var capture = Capture.CreateCameraCapture(Index))
                        {
                            try
                            {
                                capture.SetProperty(CaptureProperty.ConvertRgb, 0);

                                capture.SetProperty(CaptureProperty.Gain, SensorGain);
                                capture.SetProperty(CaptureProperty.Brightness, Exposure);
                                while (!cancellationToken.IsCancellationRequested)
                                {
                                    // Runtime settable properties
                                    if (SensorGain != lastSensorGain)
                                    {
                                        capture.SetProperty(CaptureProperty.Gain, SensorGain);
                                        lastSensorGain = SensorGain;
                                    }
                                    if (Exposure != lastExposure)
                                    {
                                        capture.SetProperty(CaptureProperty.Brightness, Exposure);
                                        lastExposure = Exposure;
                                    }

                                    var image = capture.QueryFrame();

                                    if (image == null)
                                    {
                                        observer.OnCompleted();
                                        break;
                                    }
                                    else observer.OnNext(image.Clone());
                                }
                            }
                            finally
                            {
                                capture.Close();
                                //captureProperties.Capture = null;
                            }

                        }
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            })
            .PublishReconnectable()
            .RefCount();
        }

        //[Description("Specifies the set of capture properties assigned to the camera.")]
       // public CapturePropertyCollection CaptureProperties
        //{
        //    get { return captureProperties; }
        //}

        public override IObservable<IplImage> Generate()
        {
            return source;
        }
    }
}
