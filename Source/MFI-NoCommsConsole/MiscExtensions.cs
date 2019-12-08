using System;
using System.Collections.Generic;
using System.Reflection;

namespace MoreFactionInteraction.NoCommsConsole
{
	public static class TypeExtensions
	{
		// Note: In .NET Framework 3.5 and below, IEnumerable<T> is not covariant, i.e.
		// an IEnumerable<MethodInfo> is not an IEnumerable<MethodBase>. Since this is used in HarmonyTargetMethods-annotated
		// methods, which must have IEnumerable<MethodBase>, we must return IEnumerable<MethodBase> here as well.
		public static IEnumerable<MethodBase> FindLambdaMethods(this Type targetType, Func<MethodInfo, bool> matcher)
		{
			// Lambda code is in compiler-generated non-public instance methods on compiler-generated non-public nested classes.
			foreach (var type in targetType.GetNestedTypes(BindingFlags.NonPublic))
			{
				foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if (matcher(method))
						yield return method;
				}
			}
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
