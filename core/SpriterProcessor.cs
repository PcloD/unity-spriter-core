// Copyright (c) 2015 The original author or authors
//
// This software may be modified and distributed under the terms
// of the zlib license.  See the LICENSE file for details.

using System;

namespace SpriterDotNet
{
    public static class SpriterProcessor
    {
        public static FrameData GetFrameData(SpriterAnimation first, SpriterAnimation second, float targetTime, float factor)
        {
            if (first == second) return GetFrameData(first, targetTime);

            float targetTimeSecond = targetTime / first.Length * second.Length;

            SpriterMainlineKey firstKeyA;
            SpriterMainlineKey firstKeyB;
            GetMainlineKeys(first.MainlineKeys, targetTime, out firstKeyA, out firstKeyB);

            SpriterMainlineKey secondKeyA;
            SpriterMainlineKey secondKeyB;
            GetMainlineKeys(second.MainlineKeys, targetTimeSecond, out secondKeyA, out secondKeyB);

            if (firstKeyA.BoneRefs.Length != secondKeyA.BoneRefs.Length
                || firstKeyB.BoneRefs.Length != secondKeyB.BoneRefs.Length
                || firstKeyA.ObjectRefs.Length != secondKeyA.ObjectRefs.Length
                || firstKeyB.ObjectRefs.Length != secondKeyB.ObjectRefs.Length)
                return GetFrameData(first, targetTime);

            float adjustedTimeFirst = AdjustTime(firstKeyA, firstKeyB, first.Length, targetTime);
            float adjustedTimeSecond = AdjustTime(secondKeyA, secondKeyB, second.Length, targetTimeSecond);

            SpriterSpatial[] boneInfosA = GetBoneInfos(firstKeyA, first, adjustedTimeFirst);
            SpriterSpatial[] boneInfosB = GetBoneInfos(secondKeyA, second, adjustedTimeSecond);
            SpriterSpatial[] boneInfos = null;
            if (boneInfosA != null && boneInfosB != null)
            {
                boneInfos = new SpriterSpatial[boneInfosA.Length];
                for (int i = 0; i < boneInfosA.Length; ++i)
                {
                    SpriterSpatial boneA = boneInfosA[i];
                    SpriterSpatial boneB = boneInfosB[i];
                    SpriterSpatial interpolated = Interpolate(boneA, boneB, factor, 1);
                    interpolated.Angle = MathHelper.CloserAngleLinear(boneA.Angle, boneB.Angle, factor);
                    boneInfos[i] = interpolated;
                }
            }

            SpriterMainlineKey baseKey = factor < 0.5f ? firstKeyA : firstKeyB;
            SpriterAnimation currentAnimation = factor < 0.5f ? first : second;

            FrameData frameData = new FrameData();

            for (int i = 0; i < baseKey.ObjectRefs.Length; ++i)
            {
                SpriterObjectRef objectRefFirst = baseKey.ObjectRefs[i];
                SpriterObject interpolatedFirst = GetObjectInfo(objectRefFirst, first, adjustedTimeFirst);

                SpriterObjectRef objectRefSecond = secondKeyA.ObjectRefs[i];
                SpriterObject interpolatedSecond = GetObjectInfo(objectRefSecond, second, adjustedTimeSecond);

                SpriterObject info = Interpolate(interpolatedFirst, interpolatedSecond, factor, 1);
                info.Angle = MathHelper.CloserAngleLinear(interpolatedFirst.Angle, interpolatedSecond.Angle, factor);

                if (boneInfos != null && objectRefFirst.ParentId >= 0) ApplyParentTransform(info, boneInfos[objectRefFirst.ParentId]);

                AddSpatialData(info, currentAnimation.Timelines[objectRefFirst.TimelineId], currentAnimation.Entity.Spriter, targetTime, frameData);
            }

            return frameData;
        }

        public static FrameData GetFrameData(SpriterAnimation animation, float targetTime, SpriterSpatial parentInfo = null)
        {
            SpriterMainlineKey keyA;
            SpriterMainlineKey keyB;
            GetMainlineKeys(animation.MainlineKeys, targetTime, out keyA, out keyB);

            float adjustedTime = AdjustTime(keyA, keyB, animation.Length, targetTime);

            SpriterSpatial[] boneInfos = GetBoneInfos(keyA, animation, targetTime, parentInfo);

            FrameData frameData = new FrameData();

            foreach (SpriterObjectRef objectRef in keyA.ObjectRefs)
            {
                SpriterObject interpolated = GetObjectInfo(objectRef, animation, adjustedTime);
                if (boneInfos != null && objectRef.ParentId >= 0) ApplyParentTransform(interpolated, boneInfos[objectRef.ParentId]);

                AddSpatialData(interpolated, animation.Timelines[objectRef.TimelineId], animation.Entity.Spriter, targetTime, frameData);
            }

            return frameData;
        }

        public static FrameMetadata GetFrameMetadata(SpriterAnimation first, SpriterAnimation second, float targetTime, float deltaTime, float factor)
        {
            SpriterAnimation currentAnimation = factor < 0.5f ? first : second;
            return GetFrameMetadata(currentAnimation, targetTime, deltaTime);
        }

        public static FrameMetadata GetFrameMetadata(SpriterAnimation animation, float targetTime, float deltaTime, SpriterSpatial parentInfo = null)
        {
            FrameMetadata metadata = new FrameMetadata();
            AddVariableAndTagData(animation, targetTime, metadata);
            AddEventData(animation, targetTime, deltaTime, metadata);
            AddSoundData(animation, targetTime, deltaTime, metadata);
            return metadata;
        }

        private static void AddVariableAndTagData(SpriterAnimation animation, float targetTime, FrameMetadata metadata)
        {
            if (animation.Meta == null) return;

            if (animation.Meta.Varlines != null && animation.Meta.Varlines.Length > 0)
            {
                foreach (SpriterVarline varline in animation.Meta.Varlines)
                {
                    SpriterVarDef variable = animation.Entity.Variables[varline.Def];
                    metadata.AnimationVars[variable.Name] = GetVariableValue(animation, variable, varline, targetTime);
                }
            }

            SpriterElement[] tags = animation.Entity.Spriter.Tags;
            SpriterTagline tagline = animation.Meta.Tagline;
            if (tagline != null && tagline.Keys != null && tagline.Keys.Length > 0)
            {
                SpriterTaglineKey key = LastKeyForTime<SpriterTaglineKey>(tagline.Keys, targetTime);
                if (key != null && key.Tags != null) foreach (SpriterTag tag in key.Tags) metadata.AnimationTags.Add(tags[tag.TagId].Name);
            }

            foreach (SpriterTimeline timeline in animation.Timelines)
            {
                SpriterMeta meta = timeline.Meta;
                if (meta == null) continue;

                SpriterObjectInfo objInfo = GetObjectInfo(animation, timeline.Name);

                if (meta.Varlines != null && meta.Varlines.Length > 0)
                {
                    foreach (SpriterVarline varline in timeline.Meta.Varlines)
                    {
                        SpriterVarDef variable = objInfo.Variables[varline.Def];
                        metadata.AddObjectVar(objInfo.Name, variable.Name, GetVariableValue(animation, variable, varline, targetTime));
                    }
                }

                if (meta.Tagline != null && meta.Tagline.Keys != null && meta.Tagline.Keys.Length > 0)
                {
                    SpriterTaglineKey key = LastKeyForTime<SpriterTaglineKey>(tagline.Keys, targetTime);
                    if (key != null && key.Tags != null) foreach (SpriterTag tag in key.Tags) metadata.AddObjectTag(objInfo.Name, tags[tag.TagId].Name);
                }
            }
        }

        private static SpriterObjectInfo GetObjectInfo(SpriterAnimation animation, string name)
        {
            SpriterObjectInfo objInfo = null;
            foreach (SpriterObjectInfo info in animation.Entity.ObjectInfos)
            {
                if (info.Name == name)
                {
                    objInfo = info;
                    break;
                }
            }

            return objInfo;
        }

        private static SpriterVarValue GetVariableValue(SpriterAnimation animation, SpriterVarDef varDef, SpriterVarline varline, float targetTime)
        {
            SpriterVarlineKey[] keys = varline.Keys;
            if (keys == null) return varDef.VariableValue;

            SpriterVarlineKey keyA = LastKeyForTime(keys, targetTime) ?? keys[keys.Length - 1];

            if (keyA == null) return varDef.VariableValue;

            SpriterVarlineKey keyB = GetNextXLineKey(varline.Keys, keyA, animation.Looping);

            if (keyB == null) return keyA.VariableValue;

            // float adjustedTime = keyA.Time == keyB.Time ? targetTime : AdjustTime(keyA, keyB, animation.Length, targetTime);
            float factor = GetFactor(keyA, keyB, animation.Length, targetTime);

            return Interpolate(keyA.VariableValue, keyB.VariableValue, factor);
        }

        private static void AddEventData(SpriterAnimation animation, float targetTime, float deltaTime, FrameMetadata metadata)
        {
            if (animation.Eventlines == null) return;

            float previousTime = targetTime - deltaTime;
            foreach (SpriterEventline eventline in animation.Eventlines)
            {
                foreach (SpriterKey key in eventline.Keys)
                {
                    if (IsTriggered(key, targetTime, previousTime, animation.Length)) metadata.Events.Add(eventline.Name);
                }
            }
        }

        private static void AddSoundData(SpriterAnimation animation, float targetTime, float deltaTime, FrameMetadata metadata)
        {
            if (animation.Soundlines == null) return;

            float previousTime = targetTime - deltaTime;
            foreach (SpriterSoundline soundline in animation.Soundlines)
            {
                foreach (SpriterSoundlineKey key in soundline.Keys)
                {
                    SpriterSound sound = key.SoundObject;
                    if (sound.Trigger && IsTriggered(key, targetTime, previousTime, animation.Length)) metadata.Sounds.Add(sound);
                }
            }
        }

        private static bool IsTriggered(SpriterKey key, float targetTime, float previousTime, float animationLength)
        {
            float timeA = Math.Min(previousTime, targetTime);
            float timeB = Math.Max(previousTime, targetTime);
            if (timeA > timeB)
            {
                if (timeA < key.Time) timeB += animationLength;
                else timeA -= animationLength;
            }
            return timeA <= key.Time && timeB >= key.Time;
        }


        private static void AddSpatialData(SpriterObject info, SpriterTimeline timeline, Spriter spriter, float targetTime, FrameData frameData)
        {
            switch (timeline.ObjectType)
            {
                case SpriterObjectType.Sprite:
                    frameData.SpriteData.Add(info);
                    break;
                case SpriterObjectType.Entity:
                    SpriterAnimation newAnim = spriter.Entities[info.EntityId].Animations[info.AnimationId];
                    float newTargetTime = info.T * newAnim.Length;
                    frameData.SpriteData.AddRange(GetFrameData(newAnim, newTargetTime, info).SpriteData);
                    break;
                case SpriterObjectType.Point:
                    frameData.PointData[timeline.Name] = info;
                    break;
                case SpriterObjectType.Box:
                    frameData.BoxData[timeline.ObjectId] = info;
                    break;
            }
        }

        private static SpriterSpatial[] GetBoneInfos(SpriterMainlineKey key, SpriterAnimation animation, float targetTime, SpriterSpatial parentInfo = null)
        {
            if (key.BoneRefs == null) return null;
            SpriterSpatial[] ret = new SpriterSpatial[key.BoneRefs.Length];

            for (int i = 0; i < key.BoneRefs.Length; ++i)
            {
                SpriterRef boneRef = key.BoneRefs[i];
                SpriterSpatial interpolated = GetBoneInfo(boneRef, animation, targetTime);

                if (boneRef.ParentId >= 0) ApplyParentTransform(interpolated, ret[boneRef.ParentId]);
                else if (parentInfo != null) ApplyParentTransform(interpolated, parentInfo);
                ret[i] = interpolated;
            }

            return ret;
        }

        private static float AdjustTime(SpriterKey keyA, SpriterKey keyB, float animationLength, float targetTime)
        {
            float nextTime = keyB.Time > keyA.Time ? keyB.Time : animationLength;
            float factor = GetFactor(keyA, keyB, animationLength, targetTime);
            return MathHelper.Linear(keyA.Time, nextTime, factor);
        }

        private static void GetMainlineKeys(SpriterMainlineKey[] keys, float targetTime, out SpriterMainlineKey keyA, out SpriterMainlineKey keyB)
        {
            keyA = LastKeyForTime(keys, targetTime);
            int nextKey = keyA.Id + 1;
            if (nextKey >= keys.Length) nextKey = 0;
            keyB = keys[nextKey];
        }

        private static T LastKeyForTime<T>(T[] keys, float targetTime) where T : SpriterKey
        {
            T current = null;
            foreach (T key in keys)
            {
                if (key.Time > targetTime) break;
                current = key;
            }

            return current;
        }

        private static T GetNextXLineKey<T>(T[] keys, T firstKey, bool looping) where T : SpriterKey
        {
            if (keys.Length == 1) return null;

            int keyBId = firstKey.Id + 1;
            if (keyBId >= keys.Length)
            {
                if (!looping) return null;
                keyBId = 0;
            }

            return keys[keyBId];
        }

        private static SpriterSpatial GetBoneInfo(SpriterRef spriterRef, SpriterAnimation animation, float targetTime)
        {
            SpriterTimelineKey[] keys = animation.Timelines[spriterRef.TimelineId].Keys;
            SpriterTimelineKey keyA = keys[spriterRef.KeyId];
            SpriterTimelineKey keyB = GetNextXLineKey(keys, keyA, animation.Looping);

            if (keyB == null) return Copy(keyA.BoneInfo);

            float factor = GetFactor(keyA, keyB, animation.Length, targetTime);
            return Interpolate(keyA.BoneInfo, keyB.BoneInfo, factor, keyA.Spin);
        }

        private static SpriterObject GetObjectInfo(SpriterRef spriterRef, SpriterAnimation animation, float targetTime)
        {
            SpriterTimelineKey[] keys = animation.Timelines[spriterRef.TimelineId].Keys;
            SpriterTimelineKey keyA = keys[spriterRef.KeyId];
            SpriterTimelineKey keyB = GetNextXLineKey(keys, keyA, animation.Looping);

            if (keyB == null) return Copy(keyA.ObjectInfo);

            float factor = GetFactor(keyA, keyB, animation.Length, targetTime);
            return Interpolate(keyA.ObjectInfo, keyB.ObjectInfo, factor, keyA.Spin);
        }

        private static float GetFactor(SpriterKey keyA, SpriterKey keyB, float animationLength, float targetTime)
        {
            float timeA = keyA.Time;
            float timeB = keyB.Time;

            if (timeA > timeB)
            {
                timeB += animationLength;
                if (targetTime < timeA) targetTime += animationLength;
            }

            float factor = MathHelper.ReverseLinear(timeA, timeB, targetTime);
            factor = ApplySpeedCurve(keyA, factor);
            return factor;
        }

        private static float ApplySpeedCurve(SpriterKey key, float factor)
        {
            switch (key.CurveType)
            {
                case SpriterCurveType.Instant:
                    factor = 0.0f;
                    break;
                case SpriterCurveType.Linear:
                    break;
                case SpriterCurveType.Quadratic:
                    factor = MathHelper.Curve(factor, 0.0f, key.C1, 1.0f);
                    break;
                case SpriterCurveType.Cubic:
                    factor = MathHelper.Curve(factor, 0.0f, key.C1, key.C2, 1.0f);
                    break;
                case SpriterCurveType.Quartic:
                    factor = MathHelper.Curve(factor, 0.0f, key.C1, key.C2, key.C3, 1.0f);
                    break;
                case SpriterCurveType.Quintic:
                    factor = MathHelper.Curve(factor, 0.0f, key.C1, key.C2, key.C3, key.C4, 1.0f);
                    break;
                case SpriterCurveType.Bezier:
                    factor = MathHelper.Bezier(key.C1, key.C2, key.C3, key.C4, factor);
                    break;
            }

            return factor;
        }

        private static SpriterSpatial Interpolate(SpriterSpatial a, SpriterSpatial b, float f, int spin)
        {
            return new SpriterSpatial
            {
                Angle = MathHelper.AngleLinear(a.Angle, b.Angle, spin, f),
                X = MathHelper.Linear(a.X, b.X, f),
                Y = MathHelper.Linear(a.Y, b.Y, f),
                ScaleX = MathHelper.Linear(a.ScaleX, b.ScaleX, f),
                ScaleY = MathHelper.Linear(a.ScaleY, b.ScaleY, f)
            };
        }

        private static SpriterObject Interpolate(SpriterObject a, SpriterObject b, float f, int spin)
        {
            return new SpriterObject
            {
                Angle = MathHelper.AngleLinear(a.Angle, b.Angle, spin, f),
                Alpha = MathHelper.Linear(a.Alpha, b.Alpha, f),
                X = MathHelper.Linear(a.X, b.X, f),
                Y = MathHelper.Linear(a.Y, b.Y, f),
                ScaleX = MathHelper.Linear(a.ScaleX, b.ScaleX, f),
                ScaleY = MathHelper.Linear(a.ScaleY, b.ScaleY, f),
                PivotX = a.PivotX,
                PivotY = a.PivotY,
                FileId = a.FileId,
                FolderId = a.FolderId,
                EntityId = a.EntityId,
                AnimationId = a.AnimationId,
                T = MathHelper.Linear(a.T, b.T, f)
            };
        }

        private static SpriterVarValue Interpolate(SpriterVarValue valA, SpriterVarValue valB, float factor)
        {
            return new SpriterVarValue
            {
                Type = valA.Type,
                StringValue = valA.StringValue,
                FloatValue = MathHelper.Linear(valA.FloatValue, valB.FloatValue, factor),
                IntValue = (int)MathHelper.Linear(valA.IntValue, valB.IntValue, factor)
            };
        }

        private static void ApplyParentTransform(SpriterSpatial child, SpriterSpatial parent)
        {
            float px = parent.ScaleX * child.X;
            float py = parent.ScaleY * child.Y;
            double angleRad = parent.Angle * Math.PI / 180;
            float s = (float)Math.Sin(angleRad);
            float c = (float)Math.Cos(angleRad);

            child.X = px * c - py * s + parent.X;
            child.Y = px * s + py * c + parent.Y;
            child.ScaleX *= parent.ScaleX;
            child.ScaleY *= parent.ScaleY;
            child.Angle = parent.Angle + Math.Sign(parent.ScaleX * parent.ScaleY) * child.Angle;
            child.Angle %= 360.0f;
        }

        private static SpriterSpatial Copy(SpriterSpatial info)
        {
            SpriterSpatial copy = new SpriterSpatial();
            FillFrom(copy, info);
            return copy;
        }

        private static SpriterObject Copy(SpriterObject info)
        {
            SpriterObject copy = new SpriterObject
            {
                AnimationId = info.AnimationId,
                EntityId = info.EntityId,
                FileId = info.FileId,
                FolderId = info.FolderId,
                PivotX = info.PivotX,
                PivotY = info.PivotY,
                T = info.T
            };

            FillFrom(copy, info);
            return copy;
        }

        private static void FillFrom(SpriterSpatial target, SpriterSpatial source)
        {
            target.Alpha = source.Alpha;
            target.Angle = source.Angle;
            target.ScaleX = source.ScaleX;
            target.ScaleY = source.ScaleY;
            target.X = source.X;
            target.Y = source.Y;
        }
    }
}
