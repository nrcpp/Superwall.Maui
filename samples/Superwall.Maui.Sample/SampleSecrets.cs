namespace Superwall.Maui.Sample;

/// <summary>API keys come from the git-ignored SampleSecrets.local.cs (copy SampleSecrets.example.cs).</summary>
internal static partial class SampleSecrets
{
    public static string? AppleApiKey
    {
        get
        {
            string? key = null;
            GetAppleApiKey(ref key);
            return key;
        }
    }

    public static string? AndroidApiKey
    {
        get
        {
            string? key = null;
            GetAndroidApiKey(ref key);
            return key;
        }
    }

    static partial void GetAppleApiKey(ref string? key);

    static partial void GetAndroidApiKey(ref string? key);
}
