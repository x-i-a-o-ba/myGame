using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace  Common
{
    /// <summary>
    /// 动画事件
    /// </summary>
  public class AnimationEventBehaviour : MonoBehaviour
  {
      private Animator animator;

      public UnityAction attackHandler;
      public UnityAction attackHandlerCancel;
      private void Awake()
      {
          animator = GetComponent<Animator>();
      }

      public void OnAttack()
      {
          attackHandler?.Invoke();
      }

      public void OnAttackCancel()
      {
          attackHandlerCancel?.Invoke();
      }
  } 

}