using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;


namespace  UGUI.Frame
{
    
    //定义委托数据类型
    public delegate void PointterEventHandler(PointerEventData eventData);
    
    
    /// <summary>
    /// ui事件监听器:管理所有UGUI事件，提供事件参数类
    /// </summary>
    public class UIEventListener:MonoBehaviour,IPointerDownHandler,IPointerClickHandler,IPointerUpHandler
    {
        
        //声明事件
        public event PointterEventHandler PointerClick;
        public event PointterEventHandler PointerDown;
        public event PointterEventHandler PointerUp;

        /// <summary>
        /// 通过变换组件获取事件监听器
        /// </summary>
        /// <param name="tf"></param>
        /// <returns></returns>
        public static UIEventListener GetListener(Transform tf)
        {
            UIEventListener uiEvent = tf.GetComponent<UIEventListener>();
            if (uiEvent==null)
            {
                uiEvent= tf.AddComponent<UIEventListener>();
            }
            return uiEvent;
        }
        //继承接口
        //抽象类  接口（多类抽象行为） 委托（一类抽象行为）
        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClick?.Invoke(eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerDown?.Invoke(eventData);
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            PointerUp?.Invoke(eventData);
        }
    } 

}