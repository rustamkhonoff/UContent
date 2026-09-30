using VContainer;

namespace UContent.VContainer
{
    public static class UContentVContainerExtensions
    {
        public static void RegisterUContent(this IContainerBuilder builder)
        {
            builder.Register<IContentService, AddressablesContentService>(Lifetime.Singleton);
        }

        public static void RegisterContentScope(this IContainerBuilder builder, string name = "VContainer")
        {
            builder.Register<ContentScope>(resolver => resolver.Resolve<IContentService>().CreateScope(name), Lifetime.Scoped);
        }
    }
}