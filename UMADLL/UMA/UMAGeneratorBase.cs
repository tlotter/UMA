using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UMA
{
    public abstract class UMAGeneratorBase : MonoBehaviour
    {
        public bool fitAtlas;
        public bool usePRO;
        public bool AtlasCrop;
        [NonSerialized]
        public TextureMerge textureMerge;
        public int maxPixels;
        public bool convertRenderTexture;
        public bool convertMipMaps;
        public int atlasResolution;
        public string[] textureNameList;
        public abstract void addDirtyUMA(UMAData umaToAdd);
        public abstract bool IsIdle();

        public struct AnimationState
        {
            public int stateHash;
            public float stateTime;
        }
        public static AnimationState[] TakeAnimatorSnapShot(Animator animator)
        {
            if (animator != null)
            {
                AnimationState[] snapshot = new AnimationState[animator.layerCount];
                for (int i = 0; i < animator.layerCount; i++)
                {
                    var state = animator.GetCurrentAnimatorStateInfo(i);
                    snapshot[i].stateHash = state.nameHash;
                    snapshot[i].stateTime = Mathf.Max(0, state.normalizedTime - Time.deltaTime / state.length);
                }
                return snapshot;
            }
            return null;
        }

        public static void ApplyAnimatorSnapShot(Animator animator, AnimationState[] snapshot)
        {
            if (snapshot != null)
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    animator.Play(snapshot[i].stateHash, i, snapshot[i].stateTime);
                }
                animator.Update(0);
            }
        }

        public virtual void UpdateAvatar(UMAData umaData)
        {
            if (umaData)
            {
                switch (umaData.avatarModel)
                {
                    case UMAData.AvatarModel.CreateHumanoidAvatar:
                        UpdateMecanimAvatar(umaData, true);
                        break;
                    case UMAData.AvatarModel.NoAvatar:
                        var animator = umaData.animator;
                        if (animator)
                        {
                            Object.DestroyImmediate(animator);
                        }
                        break;
                    case UMAData.AvatarModel.UseExistingAvatar:
                        break;
                    case UMAData.AvatarModel.CreateGenericAvatar:
                        UpdateMecanimAvatar(umaData, false);
                        break;
                }
            }
        }

        public virtual void UpdateMecanimAvatar(UMAData umaData, bool humanoid)
        {
            if (umaData.animationController == null) return;

            var animator = umaData.animator;

            AnimationState[] snapshot = null;
            if (animator != null && umaData.animationController == animator.runtimeAnimatorController)
            {
                snapshot = TakeAnimatorSnapShot(animator);
            }

            bool applyRootMotion = false;
            bool animatePhysics = false;
            AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (animator != null)
            {
                applyRootMotion = animator.applyRootMotion;
                animatePhysics = animator.animatePhysics;
                cullingMode = animator.cullingMode;
                Object.DestroyImmediate(animator);
            }

            var oldParent = umaData.umaRoot.transform.parent;
            umaData.umaRoot.transform.parent = null;
            var umaTPose = umaData.umaRecipe.raceData.TPose;
            umaTPose.DeSerialize();
            Avatar avatar;
            if (humanoid)
            {
                avatar = CreateHumanoidAvatar(umaData, umaTPose);
            }
            else
            {
                avatar = CreateGenericAvatar(umaData, umaTPose);
            }
            animator = CreateAnimator(umaData, avatar, umaData.animationController, applyRootMotion, animatePhysics, cullingMode);
            umaData.animator = animator;
            umaData.umaRoot.transform.parent = oldParent;
            ApplyAnimatorSnapShot(animator, snapshot);
        }

        public static Animator CreateAnimator(UMAData umaData, Avatar avatar, RuntimeAnimatorController controller, bool applyRootMotion, bool animatePhysics, AnimatorCullingMode cullingMode)
        {
            var animator = umaData.umaRoot.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = applyRootMotion;
            animator.animatePhysics = animatePhysics;
            animator.cullingMode = cullingMode;
            return animator;
        }

        public static Avatar CreateHumanoidAvatar(UMAData umaData, UmaTPose umaTPose)
        {
            HumanDescription description = CreateHumanDescription(umaData, umaTPose);
            //DebugLogHumanAvatar(umaData.umaRoot, description);
            Avatar res = AvatarBuilder.BuildHumanAvatar(umaData.umaRoot, description);
            return res;
        }

        public static Avatar CreateGenericAvatar(UMAData umaData, UmaTPose umaTPose)
        {
            Avatar res = AvatarBuilder.BuildGenericAvatar(umaData.umaRoot, umaData.genericRootMotionBone);
            return res;
        }

        public static void DebugLogHumanAvatar(GameObject root, HumanDescription description)
        {
            Debug.Log("***", root);
            Dictionary<String, String> bones = new Dictionary<String, String>();
            foreach (var sb in description.skeleton)
            {
                Debug.Log(sb.name);
                bones[sb.name] = sb.name;
            }
            Debug.Log("----");
            foreach (var hb in description.human)
            {
                string boneName;
                if (bones.TryGetValue(hb.boneName, out boneName))
                {
                    Debug.Log(hb.humanName + " -> " + boneName);
                }
                else
                {
                    Debug.LogWarning(hb.humanName + " !-> " + hb.boneName);
                }
            }
            Debug.Log("++++");
        }

        public static HumanDescription CreateHumanDescription(UMAData umaData, UmaTPose umaTPose)
        {
            var res = new HumanDescription();
            res.armStretch = 0;
            res.feetSpacing = 0;
            res.legStretch = 0;
            res.lowerArmTwist = 0.2f;
            res.lowerLegTwist = 1f;
            res.upperArmTwist = 0.5f;
            res.upperLegTwist = 0.1f;

            res.human = umaTPose.humanInfo;
            res.skeleton = umaTPose.boneInfo;
            res.skeleton[0].name = umaData.umaRoot.name;
            SkeletonModifier(umaData, ref res.skeleton);
            return res;
        }

        private static void SkeletonModifier(UMAData umaData, ref SkeletonBone[] bones)
        {
            for (var i = 0; i < bones.Length; i++)
            {
                var skeletonbone = bones[i];
                UMAData.BoneData entry;
                if (umaData.boneHashList.TryGetValue(UMASkeleton.StringToHash(skeletonbone.name), out entry))
                {
                    //var entry = umaData.boneList[skeletonbone.name];
                    skeletonbone.position = entry.boneTransform.localPosition;
                    //skeletonbone.rotation = entry.boneTransform.localRotation;
                    skeletonbone.scale = entry.boneTransform.localScale;
                    bones[i] = skeletonbone;
                }
            }
        }
    }
}
