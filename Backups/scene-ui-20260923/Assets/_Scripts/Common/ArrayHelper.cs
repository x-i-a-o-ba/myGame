using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;


namespace  Common
{
    /*
     c#的扩展方法，在不修改代码的情况下，为其增加新的功能
     但是还不会改变微软的数组类，为它增加新方法
     
     三要素：
     1.扩展方法所在的类必须是静态类
     2.在第一个参数上，使用this关键字修饰被扩展类型
     3.在另一个命名空间下
     
     作用：方便调用 
      
     */
    
    
    
    /// <summary>
    ///数组帮助类
    /// </summary>
  public static class ArrayHelper 
  {
      
      /// <summary>
      /// 查找满足条件的单个元素
      /// </summary>
      /// <param name="array">数组</param>
      /// <param name="condition">查找条件</param>
      /// <typeparam name="T">数组类型</typeparam>
      /// <returns></returns>
      public static T Find<T>(this T[] array,Func<T,bool> condition) 
      {
          for (int i = 0; i < array.Length; i++)
          {
              //满足条件 调用的者指定相应的条件
              if (condition(array[i]))
              return array[i];
              
          }
          //返回 泛型的默认值
          return default(T);
      }   

      public static T[] FindAll<T>(this T[] array,Func<T,bool> condition) 
      {
          
          List<T> list = new List<T>();
          for (int i = 0; i < array.Length; i++)
          {
              //满足条件 调用的者指定相应的条件
              if (condition(array[i]))
                  list.Add(array[i]);
              
          }
          //将集合转成数组
          return list.ToArray();
      }   
      
      /// <summary>
      /// 最大值
      /// </summary>
      /// <param name="array"></param>
      /// <param name="condition"></param>
      /// <typeparam name="T"></typeparam>
      /// <typeparam name="Q"></typeparam>
      /// <returns></returns>
      public static T GetMax<T,Q>(this T[] array,Func<T,Q> condition )where Q:IComparable
      {
          T max=array[0];
          for (int i = 0; i < array.Length; i++)
          {
              //比较的条件
              if (condition(max).CompareTo(condition(array[i]))<0)
              {
                  max = array[i]; 
              }
              
          }

          return max;
      }
      
      
      /// <summary>
      /// 最小值
      /// </summary>
      /// <param name="array"></param>
      /// <param name="condition"></param>
      /// <typeparam name="T"></typeparam>
      /// <typeparam name="Q"></typeparam>
      /// <returns></returns>
      public static T GetMin<T,Q>(this T[] array,Func<T,Q> condition )where Q:IComparable
      {
          T min=array[0];
          for (int i = 0; i < array.Length; i++)
          {
              // 比较的条件
              if (condition(min).CompareTo(condition(array[i]))>0)
              {
                  min = array[i];
              }
          }

          return min;
      }

      
      //升序
      public static void OrderBy<T,Q>(this T[] array,Func<T,Q> condition) where Q:IComparable
      {
          for (int i = 0; i < array.Length-1; i++)
          {
              for (int j = 0; j < array.Length-1-i; j++)
              {
                  if (condition(array[j]).CompareTo(array[j+1])>0)
                  {
                      T temp = array[j];
                      array[j] = array[j+1];
                      array[j+1] = temp;
                  }
              }
          }
      }
      
      //降序
      public static void OrderDescding<T,Q>(this T[] array,Func<T,Q> condition) where Q:IComparable
      {
          for (int i = 0; i < array.Length-1; i++)
          {
              for (int j = 0; j < array.Length-1-i; j++)
              {
                  if (condition(array[j]).CompareTo(array[j+1])<0)
                  {
                      T temp = array[j];
                      array[j] = array[j+1];
                      array[j+1] = temp;
                  }
              }
          }
      }
      
      //筛选
      public static Q[] Select<T,Q>(this T[] array,Func<T,Q> condition) 
      {
          
          //存储满足条件的元素
          Q[] qArray = new Q[array.Length];
          for (int i = 0; i < array.Length; i++)
          {
              //筛选的条件
             qArray[i]= condition(array[i]);
          }
          return qArray; 
      }
      
      
      
  } 

}