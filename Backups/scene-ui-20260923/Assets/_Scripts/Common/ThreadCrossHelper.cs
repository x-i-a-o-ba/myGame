using System;
using System.Collections.Generic;
using UnityEngine;

namespace  Common
{
    /// <summary>
    ///线程交叉访问助手
    /// </summary>
    public class ThreadCrossHelper : MonoSingleton<ThreadCrossHelper>
    {
        class DelayedItem
        {
            public Action CurrentAction { get; set; }
            public DateTime  Time { get; set; }
        }
        
        private List<DelayedItem> actionList;

        public override void Init()
        {
            base.Init();
            actionList = new List<DelayedItem>();
        }

        private void Update()
        {
            for (int i = actionList.Count-1; i >=0; i--)
            {
                //如果发现到达执行时间 则
                if (actionList[i].Time<=DateTime.Now)
                {
                    //执行
                    actionList[i].CurrentAction();
                    //移除
                    actionList.RemoveAt(i);
                }
            }
        }

        public void ExecuteOnMainThread(Action action, float delay = 0)
        {
            // 使用 DateTime 获取当前时间
            DateTime now = DateTime.Now;
            DateTime executeTime = now.AddSeconds(delay);

            var item = new DelayedItem { CurrentAction = action, Time = executeTime };
            actionList.Add(item);
        }
        
        
        
    }
}
