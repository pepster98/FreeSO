﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tso.common.rendering.framework.camera;
using Microsoft.Xna.Framework.Graphics;
using tso.common.rendering.framework.model;

namespace tso.common.rendering.framework
{
    public class _3DScene : _3DAbstract
    {
        private List<_3DComponent> m_Elements = new List<_3DComponent>();

        public ICamera Camera { get; set; }
        public _3DLayer Parent;
        public string ID { get; set; }

        public _3DScene(ICamera camera){
            this.Camera = camera;
        }

        public _3DScene(){
        }

        public override List<_3DComponent> GetElements()
        {
            return m_Elements;
        }

        public override void Initialize(_3DLayer layer)
        {
            this.Parent = layer;

            foreach (var element in m_Elements)
            {
                element.Initialize();
            }
        }

        public override void Update(UpdateState state)
        {
            for (int i = 0; i < m_Elements.Count; i++)
            {
                m_Elements[i].Update(state);
            }
        }

        public void Remove(_3DComponent item){
            m_Elements.Remove(item);
        }

        public override void Add(_3DComponent item)
        {
            m_Elements.Add(item);
            item.Scene = this;
            if (this.Parent != null)
            {
                item.Initialize();
            }
        }

        public virtual void PreDraw(GraphicsDevice device){
        }

        public override void Draw(GraphicsDevice device)
        {
            //RenderTarget oldRenderTarget = null;

            for (int i = 0; i < m_Elements.Count; i++)
            {
                //if(m_Elements[i] != null)
                    m_Elements[i].Draw(device);
            }

            //if (Camera.DrawCamera)
            //{
            //    Camera.Draw(device);
            //}
        }

        public override string ToString()
        {
            if (ID != null)
            {
                return ID;
            }

            return base.ToString();
        }

        public override void DeviceReset(GraphicsDevice Device)
        {
        }
    }
}
