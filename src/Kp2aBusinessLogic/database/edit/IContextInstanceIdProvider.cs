namespace keepass2android;

// A context instance can be the instance of an Activity. Even if the activity is recreated (due to a configuration change, for example), the instance id must remain the same
// but it must be different for other activities/services or if the activity is finished and then starts again.
// We want to be able to perform actions on a context instance, even though that instance might not live at the time when we want to perform the action.
// In that case, we want to be able to register the action such that it is performed when the activity is recreated.
public interface IContextInstanceIdProvider
{
    
    int ContextInstanceId { get; }


}