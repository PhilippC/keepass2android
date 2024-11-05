namespace keepass2android
{
	public interface IProgressDialog
	{
		void SetTitle(string title);
		void SetMessage(string resourceString);
		void Dismiss();
		void Show();
	}
}