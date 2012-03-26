using System;
using System.Runtime.Serialization;

namespace SisoDb.Serialization.Common
{
	internal static class DeserializeTypeRef
	{
		internal static SerializationException CreateSerializationError(Type type, string strType)
		{
			return new SerializationException(String.Format(
			"Type definitions should start with a '{0}', expecting serialized type '{1}', got string starting with: {2}",
			JsWriter.MapStartChar, type.Name, strType.Substring(0, strType.Length < 50 ? strType.Length : 50)));
		}

		/* The old Reference generic implementation
		internal static object StringToType(
			ITypeSerializer Serializer, 
			Type type, 
			string strType, 
			EmptyCtorDelegate ctorFn, 
			Dictionary<string, TypeAccessor> typeAccessorMap)
		{
			var index = 0;

			if (strType == null)
				return null;

			if (!Serializer.EatMapStartChar(strType, ref index))
				throw DeserializeTypeRef.CreateSerializationError(type, strType);

			if (strType == JsWriter.EmptyMap) return ctorFn();

			object instance = null;

			var strTypeLength = strType.Length;
			while (index < strTypeLength)
			{
				var propertyName = Serializer.EatMapKey(strType, ref index);

				Serializer.EatMapKeySeperator(strType, ref index);

				var propertyValueStr = Serializer.EatValue(strType, ref index);
				var possibleTypeInfo = propertyValueStr != null && propertyValueStr.Length > 1 && propertyValueStr[0] == '_';

				if (possibleTypeInfo && propertyName == JsWriter.TypeAttr)
				{
					var typeName = Serializer.ParseString(propertyValueStr);
					instance = ReflectionExtensions.CreateInstance(typeName);
					if (instance == null)
					{
						Tracer.Instance.WriteWarning("Could not find type: " + propertyValueStr);
					}
					else
					{
						//If __type info doesn't match, ignore it.
						if (!type.IsInstanceOfType(instance))
							instance = null;
					}

					Serializer.EatItemSeperatorOrMapEndChar(strType, ref index);
					continue;
				}

				if (instance == null) instance = ctorFn();

				TypeAccessor typeAccessor;
				typeAccessorMap.TryGetValue(propertyName, out typeAccessor);

				var propType = possibleTypeInfo ? TypeAccessor.ExtractType(Serializer, propertyValueStr) : null;
				if (propType != null)
				{
					try
					{
						if (typeAccessor != null)
						{
							var parseFn = Serializer.GetParseFn(propType);
							var propertyValue = parseFn(propertyValueStr);
							typeAccessor.SetProperty(instance, propertyValue);
						}

						Serializer.EatItemSeperatorOrMapEndChar(strType, ref index);

						continue;
					}
					catch
					{
						Tracer.Instance.WriteWarning("WARN: failed to set dynamic property {0} with: {1}", propertyName, propertyValueStr);
					}
				}

				if (typeAccessor != null && typeAccessor.GetProperty != null && typeAccessor.SetProperty != null)
				{
					try
					{
						var propertyValue = typeAccessor.GetProperty(propertyValueStr);
						typeAccessor.SetProperty(instance, propertyValue);
					}
					catch
					{
						Tracer.Instance.WriteWarning("WARN: failed to set property {0} with: {1}", propertyName, propertyValueStr);
					}
				}

				Serializer.EatItemSeperatorOrMapEndChar(strType, ref index);
			}

			return instance;
		}
		*/
	}

	//The same class above but JSON-specific to enable inlining in this hot class.
}