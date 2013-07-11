namespace keepass2android
{
	/// <summary>
	/// Keys to identify user-displayable strings.
	/// </summary>
	/// Do not rename the keys here unless you rename the corresponding keys in the resource file of KP2A.
	/// The keys are resolved by reflection to the static Resource class. This kind of duplication is necessary 
	/// in order to use the Resource mechanism of Android but still decouple the logic layer from the UI.
    public enum UiStringKey
    {
        AskDeletePermanentlyGroup,
        progress_title,
        AskDeletePermanentlyEntry,
        search_results,
        AskDeletePermanently_title,
        saving_database,
        keyfile_does_not_exist,
        RecycleBin,
        progress_create,
        loading_database,
		AddingEntry,
		AddingGroup,
		DeletingEntry,
		DeletingGroup,
		SettingPassword,
		UndoingChanges,
		TransformingKey,
		DecodingDatabase,
		ParsingDatabase,
		CheckingTargetFileForChanges,
		TitleSyncQuestion,
		MessageSyncQuestion,
		SynchronizingDatabase
    }
}