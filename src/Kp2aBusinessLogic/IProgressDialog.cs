namespace keepass2android
{
	public interface IProgressDialog
	{
		void SetTitle(string title);
		void SetMessage(string getResourceString);
		void Dismiss();
		void Show();
	}
}