using System;
using System.Collections.Generic;
//using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

using Windows.UI.Popups;
using Windows.Data.Xml.Dom;
using System.Threading.Tasks;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace DocScanDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture m_mediaCaptureMgr = null;
        private DeviceInformationCollection m_devInfoCollection;
        private bool m_bReversePreviewRotation;
        private bool m_bRotateVideoOnOrientationChange;
        private bool m_bPreviewing;
        private LowLagPhotoCapture m_lowLagPhoto = null;
        private TypedEventHandler<SystemMediaTransportControls, SystemMediaTransportControlsPropertyChangedEventArgs> m_mediaPropertyChanged;
        private Windows.Graphics.Display.DisplayOrientations m_displayOrientation;
        private double m_rotHeight;
        private double m_rotWidth;
        static Guid rotGUID = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");
        private ListView devListView = new ListView();

        /*public static class myGlobals
        {
            public static int procNum { get; private set; }

            public static void newProc(int i)
            {
                procNum = i;
            }
        }*/
      
        public MainPage()
        {
            this.InitializeComponent();
            InitSettings();
            m_mediaPropertyChanged = new TypedEventHandler<SystemMediaTransportControls, SystemMediaTransportControlsPropertyChangedEventArgs>(SystemMediaControls_PropertyChanged);
            EnumerateWebcamsAsync();
            startDevice();
            showCam();
        }

        private void InitSettings()
        {
            SystemMediaTransportControls systemMediaControls = SystemMediaTransportControls.GetForCurrentView();
            systemMediaControls.PropertyChanged += m_mediaPropertyChanged;
            Windows.Graphics.Display.DisplayInformation displayInfo = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();
            m_displayOrientation = displayInfo.CurrentOrientation;
            displayInfo.OrientationChanged += DisplayInfo_OrientationChanged;
        }

        private void DisplayInfo_OrientationChanged(Windows.Graphics.Display.DisplayInformation sender, object args)
        {
            m_displayOrientation = sender.CurrentOrientation;
            OrientationChanged();
        }

        private Windows.Storage.FileProperties.PhotoOrientation PhotoRotationLookup(
            Windows.Graphics.Display.DisplayOrientations displayOrientation,
            bool counterclockwise)
        {
            switch (displayOrientation)
            {
                case Windows.Graphics.Display.DisplayOrientations.Landscape:
                    return Windows.Storage.FileProperties.PhotoOrientation.Normal;

                case Windows.Graphics.Display.DisplayOrientations.Portrait:
                    return (counterclockwise) ? Windows.Storage.FileProperties.PhotoOrientation.Rotate90 :
                        Windows.Storage.FileProperties.PhotoOrientation.Rotate270;

                case Windows.Graphics.Display.DisplayOrientations.LandscapeFlipped:
                    return Windows.Storage.FileProperties.PhotoOrientation.Rotate180;

                case Windows.Graphics.Display.DisplayOrientations.PortraitFlipped:
                    return (counterclockwise) ? Windows.Storage.FileProperties.PhotoOrientation.Rotate270 :
                        Windows.Storage.FileProperties.PhotoOrientation.Rotate90;

                default:
                    return Windows.Storage.FileProperties.PhotoOrientation.Unspecified;
            }
        }

        private async void EnumerateWebcamsAsync()
        {
            try
            {
                m_devInfoCollection = null;

                devListView.Items.Clear();


                m_devInfoCollection = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (m_devInfoCollection.Count == 0)
                {
                    var dialog = new MessageDialog("No WebCams found!");
                    await dialog.ShowAsync();
                }
                else
                {
                    for (int i = 0; i < m_devInfoCollection.Count; i++)
                    {
                        var devInfo = m_devInfoCollection[i];
                        var location = devInfo.EnclosureLocation;

                        if (location != null)
                        {

                            if (location.Panel == Windows.Devices.Enumeration.Panel.Front)
                            {
                                devListView.Items.Add(devInfo.Name + "-Front");
                            }
                            else if (location.Panel == Windows.Devices.Enumeration.Panel.Back)
                            {
                                devListView.Items.Add(devInfo.Name + "-Back");
                            }
                            else
                            {
                                devListView.Items.Add(devInfo.Name);
                            }
                        }
                        else
                        {
                            devListView.Items.Add(devInfo.Name);
                        }
                    }
                    devListView.SelectedIndex = 1;
                    // Select the HovercCam Solo8
                   //devListView.SelectedIndex = devListView.Items.IndexOf("HoverCam Solo8");
                }
               // myGlobals.newProc(1);
            }
            catch (Exception e)
            {
                var dialog = new MessageDialog(e.ToString());
                await dialog.ShowAsync();
            }
        }

        private async void SystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs e)
        {
            switch (e.Property)
            {
                case SystemMediaTransportControlsProperty.SoundLevel:
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (sender.SoundLevel != Windows.Media.SoundLevel.Muted)
                        {
                            EnumerateWebcamsAsync();
                            m_rotHeight = CamView.Height;
                            m_rotWidth = CamView.Width;
                        }
                    });
                    break;

                default:
                    break;
            }
        }

        private uint VideoPreviewRotationLookup(
          Windows.Graphics.Display.DisplayOrientations displayOrientation, bool counterclockwise)
        {
            switch (displayOrientation)
            {
                case Windows.Graphics.Display.DisplayOrientations.Landscape:
                    return 0;

                case Windows.Graphics.Display.DisplayOrientations.Portrait:
                    {
                        if (counterclockwise)
                        {
                            return 270;
                        }
                        else
                        {
                            return 90;
                        }
                    }

                case Windows.Graphics.Display.DisplayOrientations.LandscapeFlipped:
                    return 180;

                case Windows.Graphics.Display.DisplayOrientations.PortraitFlipped:
                    {
                        if (counterclockwise)
                        {
                            return 90;
                        }
                        else
                        {
                            return 270;
                        }
                    }

                default:
                    return 0;
            }
        }

        private async void OrientationChanged()
        {
            try
            {
                if (m_mediaCaptureMgr == null)
                {
                    return;
                }

                var videoEncodingProperties = m_mediaCaptureMgr.VideoDeviceController.GetMediaStreamProperties(Windows.Media.Capture.MediaStreamType.VideoPreview);

                bool previewMirroring = m_mediaCaptureMgr.GetPreviewMirroring();
                bool counterclockwiseRotation = (previewMirroring && !m_bReversePreviewRotation) ||
                    (!previewMirroring && m_bReversePreviewRotation);

                if (m_bRotateVideoOnOrientationChange && m_bPreviewing)
                {
                    var rotDegree = VideoPreviewRotationLookup(m_displayOrientation, counterclockwiseRotation);
                    videoEncodingProperties.Properties.Add(rotGUID, rotDegree);
                    await m_mediaCaptureMgr.SetEncodingPropertiesAsync(Windows.Media.Capture.MediaStreamType.VideoPreview, videoEncodingProperties, null);
                    if (rotDegree == 90 || rotDegree == 270)
                    {
                        CamView.Height = m_rotHeight;
                        CamView.Width = m_rotWidth;
                    }
                    else
                    {
                        CamView.Height = m_rotWidth;
                        CamView.Width = m_rotHeight;
                    }
                }
            }
            catch (Exception exception)
            {
                var dialog = new MessageDialog(exception.ToString());
                await dialog.ShowAsync();
            }
        }

        private async void startDevice()
        {
           // CamView.Visibility = Visibility.Visible;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                m_bReversePreviewRotation = false;
                m_mediaCaptureMgr = new Windows.Media.Capture.MediaCapture();

                var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings();
                var chosenDevInfo = m_devInfoCollection[devListView.SelectedIndex];
                settings.VideoDeviceId = chosenDevInfo.Id;

                if (chosenDevInfo.EnclosureLocation != null && chosenDevInfo.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back)
                {
                    m_bRotateVideoOnOrientationChange = true;
                    m_bReversePreviewRotation = false;
                }
                else if (chosenDevInfo.EnclosureLocation != null && chosenDevInfo.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front)
                {
                    m_bRotateVideoOnOrientationChange = true;
                    m_bReversePreviewRotation = true;
                }
                else
                {
                    m_bRotateVideoOnOrientationChange = false;
                }
                await m_mediaCaptureMgr.InitializeAsync(settings);
                m_lowLagPhoto = await m_mediaCaptureMgr.PrepareLowLagPhotoCaptureAsync(ImageEncodingProperties.CreateJpeg());
                // await Task.Delay(TimeSpan.FromSeconds(2));
                //myGlobals.newProc(2);
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog(ex.ToString());
                await dialog.ShowAsync();
                //Debug.WriteLine(ex.ToString());
            }
        }

        private async void showCam()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4));
                CamView.Source = m_mediaCaptureMgr;
                await m_mediaCaptureMgr.StartPreviewAsync();
                //myGlobals.newProc(3);
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog(ex.ToString());
                await dialog.ShowAsync();
            }

        }

        private async void cameraBtnClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var myphoto = await m_lowLagPhoto.CaptureAsync();
                var currentRotation = GetCurrentPhotoRotation();
                var photoStorageFile = await ReencodePhotoAsync(myphoto.Frame.CloneStream(), currentRotation);
                var photoStream = await photoStorageFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                var bmpimg = new BitmapImage();
                imageView.Source = null;
                bmpimg.SetSource(photoStream);
                imageView.Source = bmpimg;

                // Play Shutter Sound
                shutterSound();

                CamView.Visibility = Visibility.Collapsed;
                imageView.Visibility = Visibility.Visible;
                cameraBtn.Visibility = Visibility.Collapsed;
                OverLayer.Visibility = Visibility.Visible;
                await Task.Delay(TimeSpan.FromSeconds(1));
                OverLayer.Visibility = Visibility.Collapsed;
                CamView.Visibility = Visibility.Visible;
                imageView.Visibility = Visibility.Collapsed;
                cameraBtn.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog(ex.ToString());
                await dialog.ShowAsync();
                //Debug.WriteLine(ex.ToString());
            }
        }

        private async Task<Windows.Storage.StorageFile> ReencodePhotoAsync(
            Windows.Storage.Streams.IRandomAccessStream stream,
            Windows.Storage.FileProperties.PhotoOrientation photoRotation)
        {
            Windows.Storage.Streams.IRandomAccessStream inputStream = null;
            Windows.Storage.Streams.IRandomAccessStream outputStream = null;
            Windows.Storage.StorageFile photoStorage = null;

            try
            {
                inputStream = stream;

                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(inputStream);

                photoStorage = await Windows.Storage.KnownFolders.PicturesLibrary.CreateFileAsync("myPhoto.jpg", Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                outputStream = await photoStorage.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);

                outputStream.Size = 0;

                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                var properties = new Windows.Graphics.Imaging.BitmapPropertySet();
                properties.Add("System.Photo.Orientation",
                    new Windows.Graphics.Imaging.BitmapTypedValue(photoRotation, Windows.Foundation.PropertyType.UInt16));

                await encoder.BitmapProperties.SetPropertiesAsync(properties);

                await encoder.FlushAsync();
            }
            finally
            {
                if (inputStream != null)
                {
                    inputStream.Dispose();
                }

                if (outputStream != null)
                {
                    outputStream.Dispose();
                }
            }

            return photoStorage;
        }

        private Windows.Storage.FileProperties.PhotoOrientation GetCurrentPhotoRotation()
        {
            bool counterclockwiseRotation = m_bReversePreviewRotation;

            if (m_bRotateVideoOnOrientationChange)
            {
                return PhotoRotationLookup(m_displayOrientation, counterclockwiseRotation);
            }
            else
            {
                return Windows.Storage.FileProperties.PhotoOrientation.Normal;
            }
        }
        private void shutterSound()
        {
            camShutter.Play();
        }

        private void camOnBtn(object sender, RoutedEventArgs e)
        {
            showCam();
        }
    }
}
