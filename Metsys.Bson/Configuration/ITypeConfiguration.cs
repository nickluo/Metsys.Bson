using System;

namespace Metsys.Bson.Configuration
{
    public interface ITypeConfiguration
    {
        ITypeConfiguration Ignore(Type type, string name);
    }

    public class TypeConfiguration : ITypeConfiguration
    {
        private readonly BsonConfiguration configuration;

        internal TypeConfiguration(BsonConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public ITypeConfiguration Ignore(Type type, string name)
        {
            configuration.AddIgnore(type, name);
            return this;
        }
    }
}
