import {
  BufferGeometry, CircleGeometry, Float32BufferAttribute, Group, Line, LineBasicMaterial,
  LineLoop, Mesh, MeshBasicMaterial, OrthographicCamera, PlaneGeometry, RingGeometry,
  Scene, Vector3, WebGLRenderer,
} from 'three';
import { accessPoints, PLAYER_RADIUS, ROOM_SIZE } from '../simulation/config';
import { radii } from '../simulation/interference';
import type { Band, Sample, SimulationConfig, SimulationState, Vec2 } from '../simulation/types';

function marker(geometry: BufferGeometry, color: number, z: number) {
  const result = new Mesh(geometry, new MeshBasicMaterial({ color }));
  result.position.z = z;
  return result;
}
function circleLine() {
  const points = Array.from({ length: 64 }, (_, i) => {
    const a = i * Math.PI / 32;
    return new Vector3(Math.cos(a), Math.sin(a), 0);
  });
  return new LineLoop(new BufferGeometry().setFromPoints(points), new LineBasicMaterial({ color: 0x427e71 }));
}
function segment() {
  const geo = new BufferGeometry();
  geo.setAttribute('position', new Float32BufferAttribute(new Float32Array(6), 3));
  const line = new Line(geo, new LineBasicMaterial());
  line.frustumCulled = false;
  return line;
}
function setSegment(line: ReturnType<typeof segment>, a: Vec2, b: Vec2, color: number) {
  const attr = line.geometry.getAttribute('position');
  attr.setXYZ(0, a.x, a.y, 0);
  attr.setXYZ(1, b.x, b.y, 0);
  attr.needsUpdate = true;
  line.material.color.setHex(color);
}
const colors: Record<Band, number> = { strong: 0xf05353, medium: 0xff9948, weak: 0xe2ce58, none: 0x54bd79 };

export function createWorld(viewport: HTMLElement) {
  const renderer = new WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x080a0a);
  viewport.append(renderer.domElement);
  const scene = new Scene();
  const camera = new OrthographicCamera(-6.2, 6.2, 6.2, -6.2, 0.1, 100);
  camera.position.z = 10;
  const half = ROOM_SIZE / 2;
  for (const side of [-half, half]) {
    const horizontal = marker(new PlaneGeometry(ROOM_SIZE + 0.035, 0.035), 0xffffff, 0);
    const vertical = marker(new PlaneGeometry(0.035, ROOM_SIZE), 0xffffff, 0);
    horizontal.position.y = side;
    vertical.position.x = side;
    scene.add(horizontal, vertical);
  }
  const labels = accessPoints.map(({ id, position: p }) => {
    const diamond = marker(new CircleGeometry(0.22, 4), 0xffffff, 0.04);
    diamond.position.set(p.x, p.y, 0.04);
    scene.add(diamond);
    const label = document.createElement('span');
    label.className = 'map-label';
    label.textContent = id;
    viewport.append(label);
    return { label, x: p.x - Math.sign(p.x) * 0.35, y: p.y - Math.sign(p.y) * 0.42 };
  });
  const player = marker(new CircleGeometry(PLAYER_RADIUS, 28), 0xffffff, 0.1);
  const ring = marker(new RingGeometry(0.20, 0.24, 40), 0xff323c, 0.2);
  const creature = marker(new RingGeometry(0.205, 0.275, 48), 0x33ffd9, 0.15);
  scene.add(player, ring, creature);

  const debug = new Group();
  debug.position.z = 0.01;
  const bands = [circleLine(), circleLine(), circleLine()];
  debug.add(...bands);
  const paths = accessPoints.map(() => {
    const apPath = segment(), normal = segment(), contamination = circleLine();
    const point = marker(new CircleGeometry(0.09, 16), 0xffffff, 0.01);
    contamination.material.color.setHex(0xf16db9);
    debug.add(apPath, normal, point, contamination);
    return { apPath, normal, point, contamination };
  });
  scene.add(debug);
  function resize() {
    const width = viewport.clientWidth, height = viewport.clientHeight;
    if (!width || !height) return;
    const aspect = width / height;
    camera.top = 6.2 / Math.min(aspect, 1);
    camera.bottom = -camera.top;
    camera.right = camera.top * aspect;
    camera.left = -camera.right;
    camera.updateProjectionMatrix();
    renderer.setSize(width, height, false);
    for (const { label, x, y } of labels) {
      label.style.left = (50 + 50 * x / camera.right) + '%';
      label.style.top = (50 - 50 * y / camera.top) + '%';
    }
  }
  new ResizeObserver(resize).observe(viewport);
  resize();
  let displayed: Sample | undefined;
  return (state: SimulationState, config: SimulationConfig, showDebug: boolean) => {
    player.position.set(state.player.x, state.player.y, 0.1);
    creature.position.set(state.creature.x, state.creature.y, 0.15);
    if (displayed !== state.sample) {
      displayed = state.sample;
      const estimate = displayed.estimate;
      ring.visible = !!estimate;
      if (estimate) ring.position.set(estimate.position.x, estimate.position.y, 0.2);
    }
    debug.visible = showDebug;
    if (showDebug) {
      const radius = radii(config);
      [radius.strong, radius.medium, radius.weak].forEach((r, i) => {
        bands[i]!.scale.setScalar(r);
        bands[i]!.position.set(state.creature.x, state.creature.y, 0);
      });
      state.sample.observations.forEach((o, i) => {
        const { apPath, normal, point, contamination } = paths[i]!;
        const { closest, level, contaminated } = o.geometry;
        setSegment(apPath, o.position, state.player, colors[level]);
        setSegment(normal, state.creature, closest, colors[level]);
        point.position.set(closest.x, closest.y, 0.01);
        point.material.color.setHex(colors[level]);
        contamination.visible = contaminated;
        contamination.scale.setScalar(config.contaminationRadius);
        contamination.position.set(o.position.x, o.position.y, 0);
      });
    }
    renderer.render(scene, camera);
  };
}
