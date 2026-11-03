// Copy to SampleSecrets.local.cs (git-ignored) and put your Superwall public API keys there.
namespace Superwall.Maui.Sample;

internal static partial class SampleSecrets
{
    static partial void GetAppleApiKey(ref string? key) => key = "pk_your_ios_key";

    static partial void GetAndroidApiKey(ref string? key) => key = "pk_your_android_key";
}
