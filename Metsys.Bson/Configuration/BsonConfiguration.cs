using System;

namespace Metsys.Bson.Configuration
{
    using System.Collections.Generic;

    public class BsonConfiguration
    {
        private readonly IDictionary<Type, IDictionary<string, string>> aliasMap = new Dictionary<Type, IDictionary<string, string>>();
        private readonly IDictionary<Type, HashSet<string>> ignored = new Dictionary<Type, HashSet<string>>();
        private readonly IDictionary<Type, HashSet<string>> ignoredIfNull = new Dictionary<Type, HashSet<string>>();
        
        //not thread safe
        private static BsonConfiguration instance;
        internal static BsonConfiguration Instance
        {
            get { return instance ?? (instance = new BsonConfiguration()); }
        }
        
        private BsonConfiguration(){}

        public static void ForType<T>(Action<ITypeConfiguration<T>> action)
        {
            action(new TypeConfiguration<T>(Instance));
        }

        public static void ForType(Action<ITypeConfiguration> action)
        {
            action(new TypeConfiguration(Instance));
        }

        
        internal void AddMap<T>(string property, string alias)
        {
            var type = typeof (T);
            if (!aliasMap.ContainsKey(type))
            {
                aliasMap[type] = new Dictionary<string, string>();
            }
            aliasMap[type][property] = alias;
        }        
        internal string AliasFor(Type type, string property)
        {            
            IDictionary<string, string> map;
            if (!aliasMap.TryGetValue(type, out map))
            {
                return property;
            }
            return map.ContainsKey(property) ? map[property] : property;
        }

        public void AddIgnore<T>(string name)
        {
            var type = typeof(T);
            AddIgnore(type,name);
        }

        public void AddIgnore(Type type, string name)
        {
            if (!ignored.ContainsKey(type))
            {
                ignored[type] = new HashSet<string>();
            }
            ignored[type].Add(name);
        }

        public bool IsIgnored(Type type, string name)
        {
            HashSet<string> list;            
            return ignored.TryGetValue(type, out list) && list.Contains(name);
        }

        public void AddIgnoreIfNull<T>(string name)
        {
            var type = typeof(T);
            if (!ignoredIfNull.ContainsKey(type))
            {
                ignoredIfNull[type] = new HashSet<string>();
            }
            ignoredIfNull[type].Add(name);
        }
        public bool IsIgnoredIfNull(Type type, string name)
        {
            HashSet<string> list;
            return ignoredIfNull.TryGetValue(type, out list) && list.Contains(name);
        }
    }
}