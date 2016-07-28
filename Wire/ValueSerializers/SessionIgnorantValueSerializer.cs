using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Wire.Compilation;

namespace Wire.ValueSerializers
{
    public abstract class SessionIgnorantValueSerializer<TElementType> : ValueSerializer
    {
        private readonly byte _manifest;
        private readonly MethodInfo _write;
        private readonly Action<Stream, object> _writeCompiled;
        private readonly MethodInfo _read;
        private readonly Func<Stream, TElementType> _readCompiled;

        protected SessionIgnorantValueSerializer(byte manifest,
            Expression<Func<Action<Stream, TElementType>>> writeStaticMethod,
            Expression<Func<Func<Stream, TElementType>>> readStaticMethod)
        {
            _manifest = manifest;
            _write = GetStatic(writeStaticMethod, typeof(void));
            _read = GetStatic(readStaticMethod, typeof(TElementType));

            var stream = Expression.Parameter(typeof(Stream));
            var value = Expression.Parameter(typeof(object));

            _writeCompiled = Expression.Lambda<Action<Stream, object>>(
                Expression.Call(_write, stream, Expression.Convert(value, typeof(TElementType))), stream, value)
                .Compile();

            _readCompiled = Expression.Lambda<Func<Stream, TElementType>>(
                Expression.Call(_read, stream), stream).Compile();
        }

        public sealed override void WriteManifest(Stream stream, SerializerSession session)
        {
            stream.WriteByte(_manifest);
        }

        public sealed override void WriteValue(Stream stream, object value, SerializerSession session)
        {
            _writeCompiled(stream, value);
        }

        public sealed override void EmitWriteValue(Compiler<ObjectWriter> c, int stream, int fieldValue, int session)
        {
            c.EmitStaticCall(_write, stream, fieldValue);
        }

        public sealed override object ReadValue(Stream stream, DeserializerSession session)
        {
            return _readCompiled(stream);
        }

        public sealed override int EmitReadValue(Compiler<ObjectReader> c, int stream, int session, FieldInfo field)
        {
            return c.StaticCall(_read, stream);
        }

        public sealed override Type GetElementType()
        {
            return typeof(TElementType);
        }
    }
}