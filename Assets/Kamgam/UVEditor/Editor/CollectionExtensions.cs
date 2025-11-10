using System.Collections.Generic;
using UnityEngine;

namespace Kamgam.UVEditor
{
    public static class Extensions
    {
        public static void AddIfNotContained<T>(this IList<T> list, T element)
        {
            if (list == null)
                return;

            if (!list.Contains(element))
            {
                list.Add(element);
            }
        }

        public static void AddIfNotContained<T>(this IList<T> list, IList<T> elements)
        {
            if (list == null)
                return;

            foreach (var e in elements)
            {
                if (!list.Contains(e))
                {
                    list.Add(e);
                }
            }
        }

        public static void RemoveAll<T>(this IList<T> list, T element)
        {
            if (list == null)
                return;

            while (list.Contains(element))
                list.Remove(element);
        }

        public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
        {
            return (collection == null || collection.Count == 0);
        }

        /// <summary>
        /// Dpes a 1 level deep check if the elements in the collection are the same (Equals() check).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool HasEqualElements<T>(this IList<T> collection, IList<T> other) where T:System.IEquatable<T>
        {
            if (collection == null && other == null)
                return true;

            if (collection == null || other == null)
                return false;

            if (collection.Count != other.Count)
                return false;

            int count = collection.Count;
            for (int i = 0; i < count; i++)
            {
                if (!collection[i].Equals(other[i]))
                    return false;
            }

            return true;
        }

        public static bool HasEqualElements(this IList<Object> collection, IList<Object> other)
        {
            if (collection == null && other == null)
                return true;

            if (collection == null || other == null)
                return false;

            if (collection.Count != other.Count)
                return false;

            int count = collection.Count;
            for (int i = 0; i < count; i++)
            {
                if (!collection[i].Equals(other[i]))
                    return false;
            }

            return true;
        }

        public static bool IsNullOrEmptyDeep<T>(this IList<T> list) where T : class
        {
            if (list == null || list.Count == 0)
                return true;

            foreach (var item in list)
            {
                if (item != null)
                    return false;
            }

            return true;
        }

        public static void LogContent<T>(this IList<T> list)
        {
            if (list == null)
                Debug.Log("Null");

            if (list.Count == 0)
                Debug.Log("Empty");

            string str = "IList["+list.Count+"]: ";
            bool first = true;
            foreach (var ele in list)
            {
                str += (first ? "" : ", ") + ele.ToString();
                first = false;
            }
            Debug.Log(str);
        }
    }
}

