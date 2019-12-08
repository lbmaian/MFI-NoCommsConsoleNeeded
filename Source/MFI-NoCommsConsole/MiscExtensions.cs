using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MoreFactionInteraction.NoCommsConsole
{
	public static class TypeExtensions
	{
		// Note: In .NET Framework 3.5 and below, IEnumerable<T> is not covariant, i.e.
		// an IEnumerable<MethodInfo> is not an IEnumerable<MethodBase>. Since this is used in HarmonyTargetMethods-annotated
		// methods, which must have IEnumerable<MethodBase>, we must return IEnumerable<MethodBase> here as well.
		public static IEnumerable<MethodBase> FindLambdaMethods(this Type targetType, Func<MethodInfo, bool> methodMatcher)
		{
			// Lambda code is in compiler-generated non-public instance methods on compiler-generated non-public nested classes.
			foreach (var type in targetType.GetNestedTypes(BindingFlags.NonPublic))
			{
				foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if (methodMatcher(method))
						yield return method;
				}
			}
		}

		public static MethodInfo FindIteratorMethod(this Type targetType, Func<Type, bool> enumeratorTypeMatcher)
		{
			// Iterator code is in non-public MoveNext method on compiler-generated non-public nested class that implements IEnumerable.
			foreach (var type in targetType.GetNestedTypes(BindingFlags.NonPublic))
			{
				if (typeof(IEnumerator).IsAssignableFrom(type) && enumeratorTypeMatcher(type))
					return type.GetMethod(nameof(IEnumerator.MoveNext), BindingFlags.Instance | BindingFlags.NonPublic);
			}
			throw new ArgumentException($"targetType ({targetType}) did not contain a matching iterator method");
		}
	}

	public static class EnumerableExtensions
	{
		public static IEnumerable<T> Append<T>(this IEnumerable<T> source, T element)
		{
			foreach (var item in source)
				yield return item;
			yield return element;
		}

		public static IEnumerable<T> Prepend<T>(this IEnumerable<T> source, T element)
		{
			yield return element;
			foreach (var item in source)
				yield return item;
		}
	}
}
