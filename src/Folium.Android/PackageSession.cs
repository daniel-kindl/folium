using System.Globalization;
using Android.Content;
using Android.Content.PM;

namespace Folium.Android;

/// <summary>Streams a verified APK into an Android package installation session.</summary>
internal static class PackageSession
{
    public static async Task CommitAsync(Context context, string path, CancellationToken cancellationToken)
    {
        string packageName = context.PackageName ?? throw new InvalidOperationException("The installed Folium package is not available.");
        PackageInstaller installer = context.PackageManager?.PackageInstaller ?? throw new InvalidOperationException("Android package installer is not available.");
        using PackageInstaller.SessionParams parameters = new(PackageInstallMode.FullInstall);
        int sessionId = installer.CreateSession(parameters);
        using PackageInstaller.Session session = installer.OpenSession(sessionId);
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            FileStream apk = File.OpenRead(path);
            await using (apk.ConfigureAwait(false))
            {
                Stream target = session.OpenWrite("folium.apk", 0, apk.Length);
                await using (target.ConfigureAwait(false))
                {
                    await apk.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                    session.Fsync(target);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            using Intent callback = new(context, typeof(UpdateInstallReceiver));
            _ = callback.SetAction($"{packageName}.UPDATE_STATUS.{sessionId.ToString(CultureInfo.InvariantCulture)}");
            using PendingIntent pending = PendingIntent.GetBroadcast(context, sessionId, callback,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable) ?? throw new InvalidOperationException("Android could not create the installer callback.");
            UpdateInstallReceiver.Register(sessionId, completion);
            CancellationTokenRegistration registration = cancellationToken.Register(
                () => _ = completion.TrySetCanceled(cancellationToken));
            await using (registration.ConfigureAwait(false))
            {
                session.Commit(pending.IntentSender);
                _ = await completion.Task.ConfigureAwait(false);
            }
        }
        catch
        {
            UpdateInstallReceiver.Remove(sessionId);
            try
            {
                session.Abandon();
            }
            catch (Java.Lang.Exception)
            {
            }

            throw;
        }
        finally
        {
            UpdateInstallReceiver.Remove(sessionId);
        }
    }
}
