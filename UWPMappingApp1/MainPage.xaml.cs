using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using UWPMappingApp1ML.Model;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPMappingApp1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void MyMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            BasicGeoposition cityPosition = new BasicGeoposition() { Latitude = 47.604, Longitude = -122.329 };
            Geopoint cityCenter = new Geopoint(cityPosition);
            
            await (sender as MapControl).TrySetViewAsync(cityCenter);
        }

        private async void QueryLocation_Click(object sender, RoutedEventArgs e)
        {

            var coordinates = await GetCoordinatesAsync(AddressBar.Text);

            SetNewMapLocation(MyMapControl, coordinates);
            
            await Task.Delay(1000);

            var mapImage = await GetMapAsImageAsync();

            PredictedOutput.Text = "Getting prediction...";

            var prediction = ConsumeModel.Predict(new ModelInput { ImageSource = mapImage.Path });

            PredictedOutput.Text = $"Prediction: {prediction.Prediction}";

            await mapImage.DeleteAsync();
        }

        private async Task<Coordinates> GetCoordinatesAsync(string address)
        {
            Coordinates result;

            using (HttpClient client = new HttpClient())
            {
                // Generate URL
                var urlEncodedAddress = HttpUtility.UrlEncode(address);
                var uri = new Uri($"https://nominatim.openstreetmap.org/search?q={urlEncodedAddress}&format=json");

                // Build Request
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Add("User-Agent", "UWP App");

                // Get Coordinates
                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                // Parse results
                var coordinates = JsonSerializer.Deserialize<IEnumerable<Coordinates>>(body).FirstOrDefault();

                if(coordinates == null)
                {
                    result = new Coordinates { Latitude = "47.604", Longitude = "-122.329" };
                    await new MessageDialog("Could not find address provided.","Address Not Found").ShowAsync();
                }
                else
                {
                    result = coordinates;
                }
            }

            return result;
        }

        private void SetNewMapLocation(MapControl map, Coordinates coordinates)
        {
            BasicGeoposition newLocation = new BasicGeoposition() { Latitude = float.Parse(coordinates.Latitude), Longitude = float.Parse(coordinates.Longitude) };
            //await MyMapControl.TrySetViewAsync(new Geopoint(newLocation));

            map.Center = new Geopoint(newLocation);
            map.ZoomLevel = 16;
        }

        private async Task<StorageFile> GetMapAsImageAsync()
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap();
            await bmp.RenderAsync(MyMapControl);

            IBuffer pixelBuffer = await bmp.GetPixelsAsync();

            StorageFolder installLocation = ApplicationData.Current.TemporaryFolder;
            StorageFile file = await installLocation.CreateFileAsync("myimage.jpeg", CreationCollisionOption.GenerateUniqueName);

            using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                DisplayInformation displayInfo = DisplayInformation.GetForCurrentView();

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)MyMapControl.RenderSize.Width,
                    (uint)MyMapControl.RenderSize.Height,
                    displayInfo.RawDpiX,
                    displayInfo.RawDpiY,
                    pixelBuffer.ToArray());

                await encoder.FlushAsync();
            }

            return file;
        }
    }



    public class Coordinates
    {
        [JsonPropertyName("lat")]
        public string Latitude { get; set; }

        [JsonPropertyName("lon")]
        public string Longitude { get; set; }
    }
}
