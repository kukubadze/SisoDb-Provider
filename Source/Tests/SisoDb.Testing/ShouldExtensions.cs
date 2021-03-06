using System;
using System.Collections;
using Machine.Specifications;
using SisoDb.NCore;
using SisoDb.NCore.Reflections;

namespace SisoDb.Testing
{
    public static class ShouldExtensions
    {
        public static void ShouldHaveTimedOut(this Exception ex)
        {
            ex.ShouldNotBeNull();
            (ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("timed out")).ShouldBeTrue();
        }

        public static void ShouldBeValueEqualTo<T>(this T x, T y)
        {
            AreValueEqual(typeof(T), x, y);
        }

        private static void AreValueEqual(Type type, object a, object b)
        {
            if (ReferenceEquals(a, b))
                return;

            if (a == null && b == null)
                return;

            if (a == null || b == null)
                a.ShouldEqual(b);

            if (type.IsEnumerableType())
            {
                var enum1 = a as IEnumerable;
                enum1.ShouldNotBeNull();

                var enum2 = b as IEnumerable;
                enum2.ShouldNotBeNull();

                var e1 = enum1.GetEnumerator();
                var e2 = enum2.GetEnumerator();

                while (e1.MoveNext() && e2.MoveNext())
                {
                    AreValueEqual(e1.Current.GetType(), e1.Current, e2.Current);
                }
                return;
            }

            if (type == typeof(object))
                throw new SpecificationException("You need to specify type to do the value equality comparision.");

            if (type.IsSimpleType())
            {
                a.ShouldEqual(b);
                return;
            }

        	var properties = type.GetProperties();
			if(properties.Length == 0)
			{
				if(!Equals(a, b))
					throw new SpecificationException("Instances of type '{0}' are not equal.".Inject(type.Name));
			}

            foreach (var propertyInfo in type.GetProperties())
            {
                var propertyType = propertyInfo.PropertyType;
                var valueForA = propertyInfo.GetValue(a, null);
                var valueForB = propertyInfo.GetValue(b, null);

                var isSimpleType = propertyType.IsSimpleType();
                if (isSimpleType)
                {
                    if(!Equals(valueForA, valueForB))
                        throw new SpecificationException("Values in property '{0}' doesn't match.".Inject(propertyInfo.Name));
                }
                else
                    AreValueEqual(propertyType, valueForA, valueForB);
            }
        }
    }
}