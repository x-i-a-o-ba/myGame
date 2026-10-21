
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Accessibility;

namespace Common
{
    /// <summary>
    ///可重置
    /// </summary>
    public interface IResetable
    {
        void OnReset();
    }
    public class GameObjectPool : MonoSingleton<GameObjectPool>
    {
    
        private Dictionary<string, List<GameObject>> cache ;

        public override void Init()
        {
            base.Init();
            cache=new Dictionary<string, List<GameObject>>();
        }


      
        //创建对象
        public GameObject CreateObject(string key ,GameObject prefab,Vector3 pos,Quaternion rot)
        {
             GameObject go=null;
             if (cache.ContainsKey(key))
             {
                 go=cache[key].Find(g => !g.activeSelf);
             }
          
            if (go == null)
            {
                go= Instantiate(prefab);
                if (!cache.ContainsKey(key))
                {
                    cache.Add(key,new List<GameObject>());
                }
                    cache[key].Add(go);
            }
            
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.SetActive(true);
            
           // go.GetComponent<IResetable>()?.OnReset();
           //遍历执行物体中所有需要重置的逻辑
            foreach (var item in go.GetComponents<IResetable>())
            {
                item?.OnReset();
            }
            
            return go;
        }
        
        //回收对象
        public void CollectObject(GameObject go,float delay)
        {
            StartCoroutine(CollectObjectDelay(go,delay));
        }

        private IEnumerator CollectObjectDelay(GameObject go,float delay)
        {
            yield return new WaitForSeconds(delay);
            go.SetActive(false);
            
        }


        /// <summary>
        /// 清空某个类别
        /// </summary>
        /// <param name="key"></param>
        public void Clear(string key)
        {
            for (int i = 0; i < cache[key].Count; i++)
            {
                Destroy(cache[key][i]);
            }
            
            cache.Remove(key);
            
            
        }


        //foreach 只读元素
        
        /// <summary>
        ///清空全部
        /// </summary>
        public void ClearAll()
        {
            foreach (var item in new List<string>(cache.Keys))
            {
                Clear(item);
            }
        }
        
    }

}
