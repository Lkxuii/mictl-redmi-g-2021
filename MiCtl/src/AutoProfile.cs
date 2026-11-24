// 温度自动切档 —— 三档 + 迟滞 + 最短保持防抖
// 固件只有三个真实档位：静谧 / 均衡 / 极速
using System;

namespace MiCtl
{
    public static class AutoProfile
    {
        public const double Hysteresis = 3.0;      // 迟滞回差 °C
        public const int MinHoldSeconds = 15;      // 同方向切换最短间隔（防抖）

        /// <summary>
        /// 根据温度与历史状态计算目标档位。
        /// temp  : CPU/GPU 中较高温度（null = 传感器失效）
        /// current: 当前实际档位
        /// lastChangeAt: 上次自动切档时间
        /// onBattery: 是否离电（固件限制离电不能极速，上限 = 均衡）
        /// 返回：目标档位；null = 保持现状
        /// </summary>
        public static MifsClient.Profile? Evaluate(
            double? temp,
            MifsClient.Profile current,
            DateTime lastChangeAt,
            bool onBattery,
            double lowThreshold,
            double highThreshold)
        {
            if (!temp.HasValue) return null; // 传感器失效 → 保持

            // 防抖：距上次自动切档太短则不动作
            if ((DateTime.Now - lastChangeAt).TotalSeconds < MinHoldSeconds) return null;

            MifsClient.Profile target;
            if (temp.Value >= highThreshold)
                target = MifsClient.Profile.FullSpeed;                       // 高温 → 极速
            else if (temp.Value <= lowThreshold - Hysteresis)
                target = MifsClient.Profile.Quiet;                           // 低温（带回差）→ 静谧
            else
                target = MifsClient.Profile.Balanced;                        // 中间 → 均衡

            // 离电上限
            if (onBattery && target == MifsClient.Profile.FullSpeed)
                target = MifsClient.Profile.Balanced;

            if (target == current) return null;
            return target;
        }
    }
}
