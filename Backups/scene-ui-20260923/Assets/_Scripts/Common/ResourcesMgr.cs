using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;



namespace  Common
{
    /// <summary>
    /// 资源管理器
    /// </summary>
    public class ResourcesMgr 
    {
        private static Dictionary<string, string> configMap;
        
       static ResourcesMgr()
        {
        
            //加载文件
            string fileContent = GetConfigFile("ConfigMap.txt");
            //解析文件
            BuildConfigMap(fileContent);
        }

        public static string GetConfigFile(string fileName)
        {
            string url;

            #region 分平台判断 StreamingAssetsPath

#if UNITY_ANDROID  //unity 宏标签
    url = "jar:file://" + Application.persistentDataPath + "!/assets/" + fileName;
#elif UNITY_EDITOR|| UNITY_STANDALONE
            url = "file://" + Application.streamingAssetsPath+"/" +fileName;
#elif UNITY_IOS
    url = "file://" + Application.persistentDataPath + "Raw/" + fileName;
#endif


                #endregion
 
            WWW www = new WWW(url);
            while (true)
            {
                if (www.isDone)
                {
                   return www.text;
                }
            }   
        }

        public static void BuildConfigMap(string fileContent)
        {
            configMap = new Dictionary<string, string>();
            
          
            //文件名=路径\r\n文件名=路径
            //字符串读取器，提供了逐行读取字符串的功能
            using(StringReader reader = new StringReader(fileContent))
            {
                string line;
                while ((line=reader.ReadLine()) != null)
                {
                    //解析行数据
                  string[] keyValue = line.Split('=');
                  configMap.Add(keyValue[0], keyValue[1]);
                  Debug.Log(keyValue[0]+"="+keyValue[1]);
                }
            }//自动调用   reader.Dispose();
        }
        
        public static T Load<T>(string prefabName) where T : UnityEngine.Object
        {
             string prefabPath=configMap[prefabName];
            return Resources.Load<T>(prefabPath); 
        }
        
    } 
}