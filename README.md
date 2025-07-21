# Ray-Tracing

这里是对光线追踪技术的第一次尝试，该项目基于Unity实现。 

感谢 Sebastian Lague 老师的开发教程及思路提供：[Code Advanture: Ray Tracing](https://www.youtube.com/watch?v=Qz0KTGYJtUk)<br>
同时也感谢老师对代码的无私开源：[SebLague/Ray-Tracing](https://github.com/SebLague/Ray-Tracing/tree/main)<br>
对本人帮助颇多  


以下为提交时间轴，用以记录学习过程： 

- 2025-7-13<br>
  实现了基本的光线追踪，添加 包围盒检测 与 分区块渲染 来对光追性能进行优化，采用 蒙特拉洛 方法对画面进行帧累积以实现降噪。  
  
- 2025-7-16<br>
  实现了 景深效果 与简单的 抗锯齿 处理。  
  
- 2025-7-20<br>
  将分块渲染进一步拓展为 BVH 结构，优化光线检测算法，极大程度提高运行帧率。  
  
- 2025-7-21<br>
  在模型的BVH分割中加入了 Surface Area Heuristic（表面积启发式）算法，使叶子节点的最大三角形数控制在个位，减少单个像素内光线需要检测的三角形数量。


TODO：<br>

