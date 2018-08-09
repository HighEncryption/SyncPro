namespace SyncPro.UI.ViewModels
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public static class ThumbnailCache
    {
        private static readonly ConcurrentDictionary<string, Thumbnail> Cache
            = new ConcurrentDictionary<string, Thumbnail>();

        public static async Task<Thumbnail> GetThumbnailsAsync(
            SyncRelationshipViewModel syncRelationship, 
            string adapterEntryId, 
            int sourceAdapterId,
            string relativePath)
        {
            string key = string.Format(
                "{0}_{1}",
                syncRelationship.GetSyncRelationship().Configuration.RelationshipId,
                adapterEntryId);

            if (!ThumbnailCache.Cache.ContainsKey(key))
            {
                byte[] result1 = await syncRelationship
                    .GetThumbnailAsync(sourceAdapterId, adapterEntryId, relativePath)
                    .ConfigureAwait(false);

                if (result1 == null || result1.Length == 0)
                {
                    ThumbnailCache.Cache[key] = null;
                }
                else
                {
                    ThumbnailCache.Cache[key] = new Thumbnail(FromByteArray(result1));
                }
            }

            return ThumbnailCache.Cache[key];
        }

        private static BitmapImage FromByteArray(byte[] data)
        {
            using (var ms = new System.IO.MemoryStream(data))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }

    public class Thumbnail
    {
        public ImageSource Image { get;}

        public Thumbnail(ImageSource image)
        {
            this.Image = image;
        }
    }
}