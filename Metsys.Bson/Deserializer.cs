using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Metsys.Bson
{
    public class Deserializer
    {
        private readonly static IDictionary<Types, Type> TypeMap = new Dictionary<Types, Type>
        {
             {Types.Int32, typeof(int)}, {Types.Int64, typeof (long)}, {Types.Boolean, typeof (bool)}, {Types.String, typeof (string)},
             {Types.Double, typeof(double)}, {Types.Binary, typeof (byte[])}, {Types.Regex, typeof (Regex)}, {Types.DateTime, typeof (DateTime)},
             {Types.ObjectId, typeof(ObjectId)}
        };
        private readonly BinaryReader reader;
        private Document current;
        private static bool guidIsLittleEndian = true;

        private Deserializer(BinaryReader reader)
        {
            this.reader = reader;
        }

        public static T Deserialize<T>(byte[] objectData, bool littleEndian = true) where T : class
        {
            guidIsLittleEndian = littleEndian;
            using (var ms = new MemoryStream())
            {
                ms.Write(objectData, 0, objectData.Length);
                ms.Position = 0;
                return Deserialize<T>(new BinaryReader(ms));
            }
        }

        public static object Deserialize(MemoryStream ms, Type type, bool littleEndian = true)
        {
            guidIsLittleEndian = littleEndian;
            ms.Position = 0;
            return Deserialize(type, new BinaryReader(ms));
        }

        private static T Deserialize<T>(BinaryReader stream)
        {
            return new Deserializer(stream).Read<T>();
        }
        private static object Deserialize(Type type, BinaryReader stream)
        {
            return new Deserializer(stream).Read(type);
        }


        private T Read<T>()
        {
            NewDocument(reader.ReadInt32());
            var @object = (T)DeserializeValue(typeof(T), Types.Object);
            return @object;
        }

        private object Read(Type type)
        {
            NewDocument(reader.ReadInt32());
            var obj = DeserializeValue(type, Types.Object);
            return obj;
        }


        private void Read(int read)
        {
            current.Digested += read;
        }

        private bool IsDone()
        {
            var isDone = current.Digested + 1 == current.Length;
            if (isDone)
            {
                reader.ReadByte(); // EOO
                var old = current;
                current = old.Parent;
                if (current != null) { Read(old.Length); }
            }
            return isDone;
        }

        private void NewDocument(int length)
        {
            var old = current;
            current = new Document { Length = length, Parent = old, Digested = 4 };
        }

        private object DeserializeValue(Type type, Types storedType)
        {
            return DeserializeValue(type, storedType, null);
        }

        private object DeserializeValue(Type type, Types storedType, object container)
        {
            if (storedType == Types.Null)
            {
                return null;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }
            if (type == typeof(string))
            {
                return ReadString();
            }
            if (type == typeof(int))
            {
                return ReadInt(storedType);
            }
            if (type.IsEnum)
            {
                return ReadEnum(type, storedType);
            }
            if (type == typeof(float))
            {
                Read(8);
                return (float)reader.ReadDouble();
            }
            if (storedType == Types.Binary)
            {
                return ReadBinary();
            }
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                return ReadList(type, container);
            }
            if (type == typeof(bool))
            {
                Read(1);
                return reader.ReadBoolean();
            }
            if (type == typeof(DateTime))
            {
                return Helper.Epoch.AddMilliseconds(ReadLong(Types.Int64));
            }
            if (type == typeof(ObjectId))
            {
                Read(12);
                return new ObjectId(reader.ReadBytes(12));
            }
            if (type == typeof(long))
            {
                return ReadLong(storedType);
            }
            if (type == typeof(double))
            {
                Read(8);
                return reader.ReadDouble();
            }
            if (type == typeof(Regex))
            {
                return ReadRegularExpression();
            }
            if (type == typeof(ScopedCode))
            {
                return ReadScopedCode();
            }
            return ReadObject(type);
        }

        private object ReadObject(Type type)
        {
            var instance = Activator.CreateInstance(type, true);
            var typeHelper = TypeHelper.GetHelperForType(type);
            while (true)
            {
                var storageType = ReadType();
                var name = ReadName();
                var isNull = false;
                if (storageType == Types.Object)
                {
                    var length = reader.ReadInt32();
                    if (length == 5)
                    {
                        reader.ReadByte(); //eoo
                        Read(5);
                        isNull = true;
                    }
                    else
                    {
                        NewDocument(length);
                    }
                }
                object container = null;
                var property = typeHelper.FindProperty(name);
                var propertyType = property != null ? property.Type : TypeMap.ContainsKey(storageType) ? TypeMap[storageType] : typeof(object);
                if (property == null && typeHelper.Expando == null)
                {
                    throw new BsonException(string.Format("Deserialization failed: type {0} does not have a property named {1}", type.FullName, name));
                }
                if (property != null && property.Setter == null)
                {
                    container = property.Getter(instance);
                }
                var value = isNull ? null : DeserializeValue(propertyType, storageType, container);
                if (property == null)
                {
                    ((IDictionary<string, object>)typeHelper.Expando.Getter(instance))[name] = value;
                }
                else if (container == null && value != null && !property.Ignored)
                {
                    property.Setter(instance, value);
                }
                if (IsDone())
                {
                    break;
                }
            }
            return instance;
        }

        private object ReadList(Type listType, object existingContainer)
        {
            if (IsDictionary(listType))
            {
                return ReadDictionary(listType, existingContainer);
            }

            NewDocument(reader.ReadInt32());
            var itemType = ListHelper.GetListItemType(listType);
            var isObject = typeof(object) == itemType;
            var wrapper = BaseWrapper.Create(listType, itemType, existingContainer);

            while (!IsDone())
            {
                var storageType = ReadType();
                ReadName();
                if (storageType == Types.Object)
                {
                    NewDocument(reader.ReadInt32());
                }
                var specificItemType = isObject ? TypeMap[storageType] : itemType;
                var value = DeserializeValue(specificItemType, storageType);
                wrapper.Add(value);
            }
            return wrapper.Collection;
        }

        private static bool IsDictionary(Type type)
        {
            var types = new List<Type>(type.GetInterfaces());
            types.Insert(0, type);
            foreach (var interfaceType in types)
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    return true;
                }
            }
            return false;
        }

        private object ReadDictionary(Type listType, object existingContainer)
        {
            var valueType = ListHelper.GetDictionarValueType(listType);
            var isObject = typeof(object) == valueType;
            var container = existingContainer == null ? ListHelper.CreateDictionary(listType, ListHelper.GetDictionarKeyType(listType), valueType) : (IDictionary)existingContainer;

            while (!IsDone())
            {
                var storageType = ReadType();

                var key = ReadName();
                if (storageType == Types.Object)
                {
                    NewDocument(reader.ReadInt32());
                }
                var specificItemType = isObject ? TypeMap[storageType] : valueType;
                var value = DeserializeValue(specificItemType, storageType);
                container.Add(key, value);
            }
            return container;
        }

        private object ReadBinary()
        {
            var length = reader.ReadInt32();
            var subType = reader.ReadByte();
            Read(5 + length);
            if (subType == 0)
            {
                return reader.ReadBytes(length);
            }
            if (subType == 2)
            {
                return reader.ReadBytes(reader.ReadInt32());
            }
            if (subType == 3 || subType == 4)
            {
                var guidNormal = new Guid(reader.ReadBytes(length));
                return guidIsLittleEndian ? guidNormal : guidNormal.Reverse();
            }
            throw new BsonException("No support for binary type: " + subType);
        }

        private string ReadName()
        {
            var buffer = new List<byte>(128); //todo: use a pool to prevent fragmentation
            byte b;
            while ((b = reader.ReadByte()) > 0)
            {
                buffer.Add(b);
            }
            Read(buffer.Count + 1);
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private string ReadString()
        {
            var length = reader.ReadInt32();
            var buffer = reader.ReadBytes(length - 1); //todo: again, look at fragementation prevention
            reader.ReadByte(); //null;
            Read(4 + length);

            return Encoding.UTF8.GetString(buffer);
        }

        private int ReadInt(Types storedType)
        {
            switch (storedType)
            {
                case Types.Int32:
                    Read(4);
                    return reader.ReadInt32();
                case Types.Int64:
                    Read(8);
                    return (int)reader.ReadInt64();
                case Types.Double:
                    Read(8);
                    return (int)reader.ReadDouble();
                default:
                    throw new BsonException("Could not create an int from " + storedType);
            }
        }

        private long ReadLong(Types storedType)
        {
            switch (storedType)
            {
                case Types.Int32:
                    Read(4);
                    return reader.ReadInt32();
                case Types.Int64:
                    Read(8);
                    return reader.ReadInt64();
                case Types.Double:
                    Read(8);
                    return (long)reader.ReadDouble();
                default:
                    throw new BsonException("Could not create an int64 from " + storedType);
            }
        }

        private object ReadEnum(Type type, Types storedType)
        {
            if (storedType == Types.Int64)
            {
                return Enum.Parse(type, ReadLong(storedType).ToString(), false);
            }
            return Enum.Parse(type, ReadInt(storedType).ToString(), false);
        }

        private object ReadRegularExpression()
        {
            var pattern = ReadName();
            var optionsString = ReadName();

            var options = RegexOptions.None;
            if (optionsString.Contains("e")) options = options | RegexOptions.ECMAScript;
            if (optionsString.Contains("i")) options = options | RegexOptions.IgnoreCase;
            if (optionsString.Contains("l")) options = options | RegexOptions.CultureInvariant;
            if (optionsString.Contains("m")) options = options | RegexOptions.Multiline;
            if (optionsString.Contains("s")) options = options | RegexOptions.Singleline;
            if (optionsString.Contains("w")) options = options | RegexOptions.IgnorePatternWhitespace;
            if (optionsString.Contains("x")) options = options | RegexOptions.ExplicitCapture;

            return new Regex(pattern, options);
        }

        private Types ReadType()
        {
            Read(1);
            return (Types)reader.ReadByte();
        }

        private ScopedCode ReadScopedCode()
        {
            reader.ReadInt32(); //length
            Read(4);
            var name = ReadString();
            NewDocument(reader.ReadInt32());
            return new ScopedCode { CodeString = name, Scope = DeserializeValue(typeof(object), Types.Object) };
        }
    }
}