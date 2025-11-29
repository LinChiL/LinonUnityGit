using UnityEngine;

public class CharacterStateManager : MonoBehaviour
{
    // 定义一个状态枚举
    public enum CharacterState
    {
        Normal,   // 正常状态，使用常规IK
        Carrying  // 搬运状态，使用搬运IK，禁用常规手部IK
    }

    // 当前状态，可以设置一个默认状态
    public CharacterState currentState = CharacterState.Normal;

    // 事件：当状态改变时通知其他脚本
    public System.Action<CharacterState> OnStateChanged;

    // 用于改变状态的方法
    public void SetState(CharacterState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            // 触发事件，通知所有订阅者
            OnStateChanged?.Invoke(newState);
        }
    }
}