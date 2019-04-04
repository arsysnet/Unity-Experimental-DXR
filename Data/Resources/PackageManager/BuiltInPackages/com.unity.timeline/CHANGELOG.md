# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)

## 2019-01-20
### Bug Fixes
- Fixed preview mode when animation clips with root curves are used (case 1116297, case 1116007)
- Added option to disable foot IK on animation playable assets (case 1115652)
- Fixed unevaluated animation tracks causing default pose (case 1109118)

## 2018-12-20
### Drag and Drop Changes
- Fixed drawing of Group Tracks when header is off-screen (case 876340)
- Fixed drag and drop of objects inside a group being inserted outside (case 1011381, case 1014774)

## 2018-11-14
### Added Signals and Markers
- Added Markers. Markers are abstract types that represent a single point in time.
- Added Signal Emitters and Signal Assets. Signal Emitters are markers that send a notification, indicated by a SignalAsset, to a GameObject indicating an event has occurred during playback of the Timeline.
- Added Signal Receiver Components. Signal Receivers are MonoBehaviour that listen for Signals from Timeline and respond by invoking UnityEvents.
- Added Signal Tracks. Signal Tracks are Timeline Tracks that are used only for Signal Emitters

## 2018-10-23
### Animate-able Properties on Track Assets
- Added API calls to access all AnimationClips used by Timeline.
- Added support in the runtime API to Animate Properties used by template-style PlayableBehaviours used as Mixers.

## [1.0.0] - 2018-10-20
### This is the first release of Timeline, as a Package
