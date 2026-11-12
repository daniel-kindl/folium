using System.Collections.Concurrent;
using Android.Content;
using Android.Content.PM;

namespace Folium.Android;

/// <summary>Receives success and failure events from Android's package installer.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class UpdateInstallReceiver : BroadcastReceiver
{
    private const int STATUS_PENDING_USER_ACTION = -1;
    private const int STATUS_SUCCESS = 0;
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> Pending = new();

    /// <summary>Registers the completion source for an install session.</summary>
    /// <param name="sessionId">The package installer session identifier.</param>
    /// <param name="completion">The operation completion source.</param>
    public static void Register(int sessionId, TaskCompletionSource<bool> completion)
    {
        Pending[sessionId] = completion;
    }

    /// <summary>Removes the callback for an abandoned package installer session.</summary>
    /// <param name="sessionId">The package installer session identifier.</param>
    public static void Remove(int sessionId)
    {
        _ = Pending.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        int sessionId = intent.GetIntExtra(PackageInstaller.ExtraSessionId, -1);
        int status = intent.GetIntExtra(PackageInstaller.ExtraStatus, 1);
        if (status == STATUS_PENDING_USER_ACTION)
        {
            Intent? confirm = GetConfirmIntent(intent);
            if (confirm is not null)
            {
                _ = confirm.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(confirm);
            }
            return;
        }

        if (Pending.TryRemove(sessionId, out TaskCompletionSource<bool>? completion))
        {
            if (status == STATUS_SUCCESS)
            {
                _ = completion.TrySetResult(true);
            }
            else
            {
                string message = intent.GetStringExtra(PackageInstaller.ExtraStatusMessage) ?? "Android could not install the update.";
                _ = completion.TrySetException(new InvalidOperationException(message));
            }
        }
    }

    private static Intent? GetConfirmIntent(Intent intent)
    {
        return OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraIntent, Java.Lang.Class.FromType(typeof(Intent))) as Intent
            : intent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
    }
}
