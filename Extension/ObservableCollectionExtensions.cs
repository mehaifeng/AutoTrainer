using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public static class ObservableCollectionExtensions
{
    private static readonly Random _random = new Random();

    /// <summary>
    /// 打乱 ObservableCollection 集合中的数据顺序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="collection">要打乱的集合</param>
    public static void Shuffle<T>(this ObservableCollection<T> collection)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        // 使用 Fisher-Yates 洗牌算法
        int n = collection.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            T value = collection[k];
            collection[k] = collection[n];
            collection[n] = value;
        }
    }

    /// <summary>
    /// 打乱 ObservableCollection 集合并返回一个新的集合
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="collection">要打乱的集合</param>
    /// <returns>打乱后的新集合</returns>
    public static ObservableCollection<T> ToShuffled<T>(this ObservableCollection<T> collection)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        var shuffledList = collection.ToList();
        int n = shuffledList.Count;

        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            T value = shuffledList[k];
            shuffledList[k] = shuffledList[n];
            shuffledList[n] = value;
        }

        return new ObservableCollection<T>(shuffledList);
    }
}