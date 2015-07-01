using System;
using System.Linq.Expressions;

namespace Metsys.Bson.Configuration
{
    public interface ITypeConfiguration<T>
    {
        ITypeConfiguration<T> UseAlias(Expression<Func<T, object>> expression, string alias);
        ITypeConfiguration<T> Ignore(Expression<Func<T, object>> expression);
        ITypeConfiguration<T> Ignore(string name);
        ITypeConfiguration<T> IgnoreIfNull(Expression<Func<T, object>> expression);
    }

    internal class TypeConfiguration<T> : ITypeConfiguration<T>
    {
        private readonly BsonConfiguration configuration;
        
        internal TypeConfiguration(BsonConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public ITypeConfiguration<T> UseAlias(Expression<Func<T, object>> expression, string alias)
        {
            var member = expression.GetMemberExpression();
            configuration.AddMap<T>(member.GetName(), alias);
            return this;
        }

        public ITypeConfiguration<T> Ignore(Expression<Func<T, object>> expression)
        {
            var member = expression.GetMemberExpression();
            return Ignore(member.GetName());
        }

        public ITypeConfiguration<T> Ignore(string name)
        {
            configuration.AddIgnore<T>(name);
            return this;
        }

        public ITypeConfiguration<T> IgnoreIfNull(Expression<Func<T, object>> expression)
        {
            var member = expression.GetMemberExpression();
            configuration.AddIgnoreIfNull<T>(member.GetName());
            return this;
        }
    }
}