using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RWCustom;
using UnityEngine;
using Random = UnityEngine.Random;

//Modified version of the vanilla fireflies
namespace Vestiges {
	public class Vestige : CosmeticInsect {
		private Vector2 dir;
		private Vector2 lastLastPos;
		private LightSource light;
		public Color col;
		public float sin;
		public Vector2 target;
		public bool exists;
		public bool initSprite = false;
		public int spriteIndex = 0;
		BepInEx.Logging.ManualLogSource Logger;

		public void SetupLogger(BepInEx.Logging.ManualLogSource newLogger) {
			Logger = newLogger;
		}

		public Vestige(Room room, Vector2 pos, Color colour) : base(room, pos, PluginEnums.Vestige) {
			lastLastPos = pos;
			sin = Random.value;
			col = colour;
			target = pos;
			exists = true;
			this.room = room;
		}

		public override void Reset(Vector2 resetPos) {
			base.Reset(resetPos);
			lastLastPos = resetPos;
		}

		public override void EmergeFromGround(Vector2 emergePos) {
			base.EmergeFromGround(emergePos);
			dir = new Vector2(0f, 1f);
		}

		public override void Update(bool eu) {
			if (room == null) {
				exists = false;
				Destroy();
				return;
			}
			vel *= 0.95f;
			vel.x = vel.x + dir.x * 0.3f;
			vel.y = vel.y + dir.y * 0.2f;
			//dir = Vector2.Lerp(dir, Custom.DegToVec(Random.value * 360f) * Mathf.Pow(Random.value, 0.75f), 0.4f).normalized;
			dir = Vector2.Lerp(dir, Custom.DirVec(pos, target), Mathf.Pow(Random.value, 2f) * 0.65f);

			if (wantToBurrow) {
				dir = Vector2.Lerp(dir, new Vector2(0f, -1f), 0.1f);
			}// else if (base.OutOfBounds) {
			 //	dir = Vector2.Lerp(dir, Custom.DirVec(pos, mySwarm.placedObject.pos), Mathf.InverseLerp(mySwarm.insectGroupData.Rad, mySwarm.insectGroupData.Rad + 100f, Vector2.Distance(pos, mySwarm.placedObject.pos)));
			 //}

			float num = TileScore(room.GetTilePosition(pos));
			IntVector2 intVector = new IntVector2(0, 0);
			for (int i = 0; i < 4; i++) {
				if (!room.GetTile(room.GetTilePosition(pos) + Custom.fourDirections[i]).Solid && TileScore(room.GetTilePosition(pos) + Custom.fourDirections[i] * 3) > num) {
					num = TileScore(room.GetTilePosition(pos) + Custom.fourDirections[i] * 3);
					intVector = Custom.fourDirections[i];
				}
			}
			vel += intVector.ToVector2() * 0.4f;

			if (room.PointSubmerged(pos)) {
				pos.y = room.FloatWaterLevel(pos.x);
			}
			sin += 1f / Mathf.Lerp(20f, 80f, Random.value);

			if (room.Darkness(pos) > 0f) {
				if (light == null) {
					light = new LightSource(pos, false, col, this);
					light.noGameplayImpact = ModManager.MMF;
					room.AddObject(light);
				}
				light.setPos = new Vector2?(pos);
				light.setAlpha = new float?(0.15f - 0.1f * Mathf.Sin(sin * 3.14159274f * 2f));
				light.setRad = new float?(60f + 20f * Mathf.Sin(sin * 3.14159274f * 2f));
			} else if (light != null) {
				light.Destroy();
				light = null;
			}

			lastLastPos = lastPos;
			base.Update(eu);

			if (!room.BeingViewed) {
				exists = false;
				Destroy();
			}
		}

		private float TileScore(IntVector2 tile) {
			if (!room.readyForAI || !room.IsPositionInsideBoundries(tile)) {
				return 0f;
			}
			return Random.value / (float)Math.Abs(room.aimap.getAItile(tile).floorAltitude - 4) / (float)Math.Abs(room.aimap.getAItile(tile).terrainProximity - 4);
		}

		public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam) {
			//sLeaser.sprites = new FSprite[1];
			spriteIndex = sLeaser.sprites.Length;
			Logger.LogInfo(sLeaser.sprites.Length);
			FSprite[] newSprite = new FSprite[] { new FSprite("pixel", true) };
			sLeaser.sprites = sLeaser.sprites.Concat(newSprite).ToArray();

			sLeaser.sprites[spriteIndex].scaleX = 2f;
			sLeaser.sprites[spriteIndex].anchorY = 0f;
			sLeaser.sprites[spriteIndex].color = col;
			AddToContainer(sLeaser, rCam, null);
			initSprite = true;
			Logger.LogInfo(sLeaser.sprites.Length);
		}

		public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
			Logger.LogInfo(spriteIndex);
			sLeaser.sprites[spriteIndex].x = Mathf.Lerp(lastPos.x, pos.x, timeStacker) - camPos.x;
			sLeaser.sprites[spriteIndex].y = Mathf.Lerp(lastPos.y, pos.y, timeStacker) - camPos.y;
			sLeaser.sprites[spriteIndex].rotation = Custom.AimFromOneVectorToAnother(Vector2.Lerp(lastLastPos, lastPos, timeStacker), Vector2.Lerp(lastPos, pos, timeStacker));
			sLeaser.sprites[spriteIndex].scaleY = Mathf.Max(2f, 2f + 1.1f * Vector2.Distance(Vector2.Lerp(lastLastPos, lastPos, timeStacker), Vector2.Lerp(lastPos, pos, timeStacker)));
			base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
		}

		public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) {
		}

		public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner) {
			base.AddToContainer(sLeaser, rCam, newContatiner);
		}

	}
}