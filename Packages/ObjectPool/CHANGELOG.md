# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2022-02-17

### This is the first release of *\<ObjectPool\>*.
* 将对象池 List 改为 Queue 
* 将脚本结构使用 region 进行了整理
* 重构文件夹结构

## [1.1.0] - 2023-10-11
* 使用 Destory 而不是 DestoryImmediate							
* 修复重复对一个示例 Recycle 会导致意外销毁的问题			
* 新增 Container 节点，用于存放回收的对象，这样可以在不干预生成的对象的Active状态的同时保证正常的 OnEnable OnDisbale 回调
