using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace ExpImageProcessing.Helpers
{
    public class CameraCapture
    {
        public MediaCapture MediaCapture { get; private set; }= new MediaCapture();

        public bool IsInitialized = false;

        public uint FrameWidth { get; private set; }
        public uint FrameHeight { get; private set; }

        public bool IsPreviewActive { get; private set; } = false;
        public async Task Initialize(CaptureElement captureElement)
        {
            if (!IsInitialized)
            {
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video
                };

                try
                {
                    await MediaCapture.InitializeAsync(settings);
                    GetVideoProperties();
                    if (captureElement != null)
                    {
                        captureElement.Source = MediaCapture;
                        IsInitialized = true;
                    }
                }
                catch (Exception )
                {
                    IsInitialized = false;
                }
            }
        }

        public async Task Start()
        {
            if (IsInitialized)
            {
                if (!IsPreviewActive)
                {
                    await MediaCapture.StartPreviewAsync();
                    IsPreviewActive = true;
                }
            }
        }

        public async Task Stop()
        {
            if (IsInitialized)
            {
                if (IsPreviewActive)
                {
                    await MediaCapture.StopPreviewAsync();
                    IsPreviewActive = false;
                }
            }
            
        }

        public async Task<SoftwareBitmap> CapturePhotoToSoftwareBitMap()
        {

            var imageEncodingProperties = ImageEncodingProperties.CreateBmp();
            var memoryStream = new InMemoryRandomAccessStream();
            await MediaCapture.CapturePhotoToStreamAsync(imageEncodingProperties, memoryStream);
            var bitMapDecoder = await BitmapDecoder.CreateAsync(memoryStream);
            return await bitMapDecoder.GetSoftwareBitmapAsync();
        }



        private void GetVideoProperties()
        {
            if (MediaCapture != null)
            {
                var videoEncodingProperties =
                    MediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as
                        VideoEncodingProperties;
                if (videoEncodingProperties != null)
                {
                    FrameWidth = videoEncodingProperties.Width;
                    FrameHeight = videoEncodingProperties.Height;
                }
                else
                {
                    Debug.WriteLine("Unable to capture videoEncoding properties");
                }
            }
        }
    }
}
