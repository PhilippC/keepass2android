using Android.Content;

namespace keepass2android.services.AutofillBase
{
    
    internal class Kp2aDigitalAssetLinksDataSource
    {
        private static Kp2aDigitalAssetLinksDataSource instance;

        private Kp2aDigitalAssetLinksDataSource() { }

        public static Kp2aDigitalAssetLinksDataSource Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Kp2aDigitalAssetLinksDataSource();
                }
                return instance;
            }
        }

        public bool IsValid(Context context, string webDomain, string packageName)
        {
            //TODO implement
            return true;
        }
    }
}