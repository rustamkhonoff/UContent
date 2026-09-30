using UnityEngine.AddressableAssets;

namespace UContent
{
    internal static class ContentMergeModeExtensions
    {
        public static Addressables.MergeMode ToAddressables(this ContentMergeMode mode)
        {
            return mode switch
            {
                ContentMergeMode.UseFirst => Addressables.MergeMode.UseFirst,
                ContentMergeMode.Union => Addressables.MergeMode.Union,
                ContentMergeMode.Intersection => Addressables.MergeMode.Intersection,
                _ => Addressables.MergeMode.Union
            };
        }
    }
}