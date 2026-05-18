import {
  Component, OnInit, OnDestroy, AfterViewInit,
  ViewChild, ElementRef, HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { ApiService } from '../../../core/services/api.service';
import { Building, Room, FloorPlanNode, FloorPlanEdge, FloorPlanNodeType } from '../../../core/models';

type EditorMode = 'select' | 'place' | 'edge' | 'delete';

interface EditorNode extends FloorPlanNode { selected: boolean; }

const NODE_RADIUS = 22;

const NODE_COLORS: Record<string, string> = {
  [FloorPlanNodeType.Room]:      '#1565c0',
  [FloorPlanNodeType.Staircase]: '#e65100',
  [FloorPlanNodeType.Elevator]:  '#6a1b9a',
  [FloorPlanNodeType.Entrance]:  '#2e7d32',
  [FloorPlanNodeType.Corridor]:  '#757575',
};
const NODE_ICONS: Record<string, string> = {
  [FloorPlanNodeType.Room]:      'A',
  [FloorPlanNodeType.Staircase]: 'S',
  [FloorPlanNodeType.Elevator]:  'E',
  [FloorPlanNodeType.Entrance]:  '⬟',
  [FloorPlanNodeType.Corridor]:  'C',
};

@Component({
  selector: 'app-floor-plan-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatButtonModule, MatIconModule, MatSelectModule,
    MatFormFieldModule, MatInputModule, MatSnackBarModule, MatTooltipModule,
    MatProgressSpinnerModule, MatDividerModule,
  ],
  template: `
<div class="editor-page">
  <div class="page-header">
    <h1>Редактор планировок<span class="draft-badge" *ngIf="dirty"> ● черновик</span></h1>
    <div class="header-controls">
      <mat-form-field appearance="outline" class="building-select">
        <mat-label>Корпус</mat-label>
        <mat-select name="bld" [(ngModel)]="selectedBuildingId" (ngModelChange)="onBuildingChange($event)">
          <mat-option *ngFor="let b of buildings" [value]="b.id">
            {{ b.shortCode }} — {{ b.address }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" class="scale-field">
        <mat-label>Масштаб (м/100px)</mat-label>
        <input matInput name="scale" type="number" [(ngModel)]="scale" min="0.1" step="0.5">
      </mat-form-field>
    </div>
  </div>

  <div class="editor-container" *ngIf="selectedBuilding; else noBuilding">
    <div class="floor-tabs">
      <button *ngFor="let f of floors" class="floor-tab"
              [class.active]="f === currentFloor" (click)="selectFloor(f)">
        {{ floorLabel(f) }}
      </button>
    </div>

    <div class="toolbar">
      <div class="btn-group">
        <button mat-stroked-button [class.active-tool]="mode==='select'" (click)="setMode('select')"
                matTooltip="Выбор / перемещение  [Esc]"><mat-icon>mouse</mat-icon></button>
        <button mat-stroked-button [class.active-tool]="mode==='edge'" (click)="setMode('edge')"
                matTooltip="Нарисовать путь  [E]"><mat-icon>timeline</mat-icon></button>
        <button mat-stroked-button [class.active-tool]="mode==='delete'" (click)="setMode('delete')"
                matTooltip="Удалить  [Del]"><mat-icon>delete</mat-icon></button>
      </div>
      <mat-divider [vertical]="true" class="v-divider"></mat-divider>
      <div class="btn-group place-types">
        <button *ngFor="let nt of nodeTypes" mat-stroked-button
                [class.active-tool]="mode==='place' && placeType===nt.type"
                (click)="setPlaceType(nt.type)"
                [style.border-color]="nt.color"
                [matTooltip]="nt.label">
          <span [style.color]="nt.color">{{ nt.label }}</span>
        </button>
      </div>
      <mat-divider [vertical]="true" class="v-divider"></mat-divider>
      <div class="btn-group">
        <button mat-icon-button (click)="zoom(1)" matTooltip="Увеличить  [Ctrl+Колесо ↑]"><mat-icon>zoom_in</mat-icon></button>
        <button mat-icon-button (click)="zoom(-1)" matTooltip="Уменьшить  [Ctrl+Колесо ↓]"><mat-icon>zoom_out</mat-icon></button>
        <button mat-icon-button (click)="resetView()" matTooltip="Сбросить вид"><mat-icon>fit_screen</mat-icon></button>
      </div>
      <span class="edge-hint" *ngIf="mode==='edge' && edgeSource">
        <mat-icon>arrow_forward</mat-icon> нажмите второй узел  [Esc — отмена]
      </span>
      <span class="spacer"></span>
      <button mat-button (click)="reload()" [disabled]="loading" matTooltip="Сбросить черновик, перезагрузить с сервера">
        <mat-icon>refresh</mat-icon>
      </button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="saving || !selectedBuildingId">
        <mat-icon>save</mat-icon> {{ saving ? 'Сохранение…' : 'Сохранить' }}
      </button>
    </div>

    <div class="main-area">
      <div class="canvas-wrapper" #canvasWrapper
           (mousemove)="onMouseMove($event)"
           (mouseup)="onMouseUp($event)"
           (mouseleave)="onMouseLeave($event)">
        <svg #svgCanvas width="100%" height="100%"
             class="floor-canvas" preserveAspectRatio="none"
             [attr.viewBox]="viewBox"
             [style.cursor]="cursor"
             (mousedown)="onCanvasMouseDown($event)">
          <defs>
            <pattern id="fp-grid" width="50" height="50" patternUnits="userSpaceOnUse">
              <path d="M50 0L0 0 0 50" fill="none" stroke="#e8e8e8" stroke-width="1"/>
            </pattern>
          </defs>
          <rect x="-9999" y="-9999" width="19998" height="19998" fill="url(#fp-grid)" pointer-events="none"/>

          <!-- Edges -->
          <g *ngFor="let e of currentFloorEdges()">
            <line [attr.x1]="nodeById(e.fromNodeId)?.x??0" [attr.y1]="nodeById(e.fromNodeId)?.y??0"
                  [attr.x2]="nodeById(e.toNodeId)?.x??0"   [attr.y2]="nodeById(e.toNodeId)?.y??0"
                  class="edge-line"
                  [class.cross-floor]="isEdgeCrossFloor(e)"
                  [class.sel-edge]="selectedEdgeId===e.id"
                  (mousedown)="onEdgeMouseDown($event,e)"/>
            <text [attr.x]="edgeMidX(e)" [attr.y]="edgeMidY(e)-7"
                  class="edge-lbl" text-anchor="middle">{{ e.distanceMeters }}м</text>
          </g>

          <!-- Edge preview -->
          <line *ngIf="edgeSource && mousePos"
                [attr.x1]="nodeById(edgeSource.nodeId)?.x??0" [attr.y1]="nodeById(edgeSource.nodeId)?.y??0"
                [attr.x2]="mousePos.x" [attr.y2]="mousePos.y"
                class="edge-preview"/>

          <!-- Nodes -->
          <g *ngFor="let node of currentFloorNodes()"
             [attr.transform]="'translate('+node.x+','+node.y+')'"
             class="node-g"
             [class.sel-node]="node.selected"
             (mousedown)="onNodeMouseDown($event,node)">
            <circle [attr.r]="NR"
                    [attr.fill]="nodeColor(node)"
                    [attr.stroke]="node.selected ? '#ff6f00' : (isUnlinkedRoom(node) ? '#f44336' : '#fff')"
                    stroke-width="2.5"/>
            <text text-anchor="middle" dy="0.35em" class="nicon">{{ nodeIcon(node) }}</text>
            <text text-anchor="middle" dy="2.5em"  class="nlbl">{{ nodeDisplayLabel(node) }}</text>
            <circle *ngIf="hasCrossFloorEdge(node)" cx="16" cy="-16" r="7" fill="#ff6f00" stroke="#fff" stroke-width="1.5"/>
            <text   *ngIf="hasCrossFloorEdge(node)" x="16" y="-12" text-anchor="middle" font-size="9" fill="#fff">↕</text>
          </g>
        </svg>
      </div>

      <!-- Props panel -->
      <div class="props-panel">
        <ng-container *ngIf="selectedNode as node">
          <div class="panel-title">Узел</div>
          <div class="prop-row">
            <span class="prop-lbl">Тип</span>
            <mat-select name="ntype" [(ngModel)]="node.nodeType" (ngModelChange)="onNodeTypeChange(node)" class="prop-sel">
              <mat-option *ngFor="let nt of nodeTypes" [value]="nt.type">
                <span [style.color]="nt.color">{{ nt.label }}</span>
              </mat-option>
            </mat-select>
          </div>
          <div class="prop-row">
            <span class="prop-lbl">Этаж</span>
            <input name="nfloor" type="number" [(ngModel)]="node.floor" class="prop-input" (change)="markDirty()">
          </div>
          <div class="prop-row" *ngIf="node.nodeType===FNT.Room">
            <span class="prop-lbl">Аудитория</span>
            <mat-select name="nroom" [(ngModel)]="node.roomId" (ngModelChange)="onRoomAssigned(node,$event)" class="prop-sel">
              <mat-option [value]="null">— без привязки —</mat-option>
              <mat-option *ngFor="let r of buildingRooms" [value]="r.id">{{ r.number }}</mat-option>
            </mat-select>
            <span class="warn" *ngIf="isUnlinkedRoom(node)">⚠ нет привязки/метки</span>
          </div>
          <div class="prop-row">
            <span class="prop-lbl">Метка</span>
            <input name="nlabel" type="text" [(ngModel)]="node.label" class="prop-input"
                   placeholder="Необязательно" (input)="markDirty()">
          </div>
          <div class="prop-row">
            <span class="prop-lbl">X / Y</span>
            <span class="prop-val">{{ node.x|number:'1.0-0' }} / {{ node.y|number:'1.0-0' }}</span>
          </div>

          <ng-container *ngIf="isMultiFloorType(node)">
            <mat-divider style="margin:10px 0 8px"></mat-divider>
            <div class="prop-lbl">Присутствует на этажах</div>
            <div class="floor-checks">
              <label *ngFor="let f of floors" class="fcheck">
                <input type="checkbox"
                       [checked]="isStairOnFloor(node,f)"
                       [disabled]="node.floor===f"
                       (change)="toggleStairFloor(node,f,$any($event.target).checked)">
                <span>{{ floorLabel(f) }}</span>
              </label>
            </div>
            <button mat-stroked-button class="full-btn" (click)="extendToAllFloors(node)">
              <mat-icon>layers</mat-icon> Добавить на все этажи
            </button>
          </ng-container>

          <button mat-stroked-button color="warn" class="full-btn del-btn" (click)="deleteNode(node)">
            <mat-icon>delete</mat-icon> Удалить узел
          </button>
        </ng-container>

        <ng-container *ngIf="selectedEdgeId && !selectedNode">
          <div class="panel-title">Путь</div>
          <ng-container *ngIf="selectedEdge() as edge">
            <div class="prop-row">
              <span class="prop-lbl">Расстояние (м)</span>
              <input name="edist" type="number" [(ngModel)]="edge.distanceMeters"
                     class="prop-input" min="1" (change)="markDirty()">
            </div>
            <div class="prop-row">
              <span class="prop-lbl">Этажи</span>
              <span class="prop-val">
                {{ nodeById(edge.fromNodeId)?.floor }} → {{ nodeById(edge.toNodeId)?.floor }}
              </span>
            </div>
            <button mat-stroked-button color="warn" class="full-btn del-btn" (click)="deleteEdge(edge)">
              <mat-icon>delete</mat-icon> Удалить путь
            </button>
          </ng-container>
        </ng-container>

        <div class="hint-area" *ngIf="!selectedNode && !selectedEdgeId">
          <div class="hint-row">
            <mat-icon class="hint-icon">info</mat-icon>
            <div class="hint-texts">
              <p *ngIf="mode==='select'">Нажмите для выбора. Перетащите для перемещения.<br>Alt+Drag или колесо — панорама. Ctrl+Колесо — масштаб.</p>
              <p *ngIf="mode==='place'">Нажмите на холст — добавится <b>{{ placeTypeLabel }}</b>. После размещения режим возвращается к «Выбор».</p>
              <p *ngIf="mode==='edge'">Нажмите первый узел, затем второй. Esc — отмена.</p>
              <p *ngIf="mode==='delete'">Нажмите узел или путь. Del — удалить выбранное.</p>
            </div>
          </div>
          <mat-divider style="margin:8px 0"></mat-divider>
          <div class="legend">
            <div *ngFor="let nt of nodeTypes" class="legend-item">
              <span class="ldot" [style.background]="nt.color"></span>{{ nt.label }}
            </div>
          </div>
          <mat-divider style="margin:8px 0"></mat-divider>
          <div class="shortcuts">
            <div><kbd>Del</kbd> удалить выбранное</div>
            <div><kbd>Esc</kbd> отмена / выбор</div>
            <div><kbd>Ctrl+S</kbd> сохранить</div>
            <div><kbd>Ctrl+↕ колесо</kbd> масштаб</div>
            <div><kbd>Alt+Drag</kbd> панорама</div>
          </div>
        </div>
      </div>
    </div>

    <div class="loading-overlay" *ngIf="loading">
      <mat-spinner diameter="48"></mat-spinner>
    </div>
  </div>

  <ng-template #noBuilding>
    <div class="no-bld">
      <mat-icon>apartment</mat-icon>
      <p>Выберите корпус для редактирования планировки</p>
    </div>
  </ng-template>
</div>
  `,
  styles: [`
    :host { display: block; }

    .editor-page {
      height: calc(100vh - 112px);
      display: flex; flex-direction: column; overflow: hidden;
    }
    .page-header {
      display: flex; align-items: center; justify-content: space-between;
      flex-shrink: 0; margin-bottom: 10px;
    }
    .page-header h1 { margin: 0; font-size: 20px; display: flex; align-items: center; gap: 8px; }
    .draft-badge { font-size: 13px; color: #e65100; font-weight: 400; }
    .header-controls { display: flex; gap: 12px; align-items: center; }
    .building-select { min-width: 260px; }
    .scale-field { width: 145px; }

    .floor-tabs {
      display: flex; gap: 4px; flex-wrap: wrap; flex-shrink: 0; margin-bottom: 6px;
    }
    .floor-tab {
      padding: 3px 12px; border: 1px solid #bbb; border-radius: 16px;
      background: #fff; cursor: pointer; font-size: 12px; transition: all 0.12s;
    }
    .floor-tab:hover { background: #e3f2fd; border-color: #1565c0; }
    .floor-tab.active { background: #1565c0; color: #fff; border-color: #1565c0; font-weight: 600; }

    .toolbar {
      display: flex; align-items: center; gap: 8px; flex-shrink: 0;
      flex-wrap: wrap; padding: 4px 0 6px;
    }
    .btn-group { display: flex; gap: 3px; }
    .place-types button { font-size: 12px; }
    .v-divider { height: 30px !important; margin: 0 2px; }
    .active-tool { background: #e3f2fd !important; border-color: #1565c0 !important; }
    .edge-hint { display: flex; align-items: center; gap: 4px; font-size: 12px; color: #e65100; font-weight: 600; }
    .spacer { flex: 1; }

    .editor-container {
      flex: 1; display: flex; flex-direction: column;
      overflow: hidden; min-height: 0; position: relative;
    }
    .main-area {
      flex: 1; display: flex; gap: 12px; overflow: hidden; min-height: 0;
    }

    .canvas-wrapper { flex: 1; overflow: hidden; position: relative; min-width: 0; }
    .floor-canvas {
      width: 100%; height: 100%; display: block;
      background: #fafafa; border: 1px solid #ddd; border-radius: 4px;
      user-select: none;
    }

    .edge-line { stroke: #90a4ae; stroke-width: 2.5; cursor: pointer; }
    .edge-line:hover { stroke: #455a64; stroke-width: 3.5; }
    .edge-line.sel-edge { stroke: #ff6f00; stroke-width: 3; }
    .edge-line.cross-floor { stroke-dasharray: 6 4; stroke: #7986cb; }
    .edge-preview { stroke: #ff8a65; stroke-width: 2; stroke-dasharray: 5 3; pointer-events: none; }
    .edge-lbl { font-size: 11px; fill: #546e7a; pointer-events: none; }

    .node-g { cursor: pointer; }
    .node-g:hover circle { opacity: 0.85; }
    .nicon { font-size: 12px; fill: #fff; pointer-events: none; font-family: monospace; font-weight: bold; }
    .nlbl  { font-size: 10px; fill: #37474f; pointer-events: none; }
    .sel-node circle { filter: drop-shadow(0 0 5px #ff6f00); }

    .props-panel {
      width: 216px; flex-shrink: 0; overflow-y: auto;
      display: flex; flex-direction: column; gap: 0;
    }
    .panel-title { font-size: 13px; font-weight: 600; margin-bottom: 10px; }
    .prop-row { display: flex; flex-direction: column; margin-bottom: 8px; gap: 3px; }
    .prop-lbl { font-size: 10px; color: #666; font-weight: 700; text-transform: uppercase; letter-spacing: 0.3px; }
    .prop-val { font-size: 12px; color: #333; }
    .prop-input {
      border: 1px solid #ccc; border-radius: 4px; padding: 4px 6px;
      font-size: 13px; width: 100%; box-sizing: border-box;
    }
    .prop-sel { font-size: 13px; }
    .warn { font-size: 11px; color: #f44336; }
    .full-btn { width: 100%; margin-top: 6px; }
    .del-btn { margin-top: 10px; }

    .floor-checks { display: flex; flex-wrap: wrap; gap: 4px; margin: 4px 0 6px; }
    .fcheck { display: flex; align-items: center; gap: 3px; font-size: 11px; cursor: pointer; }
    .fcheck input { cursor: pointer; }

    .hint-area { display: flex; flex-direction: column; }
    .hint-row { display: flex; gap: 8px; align-items: flex-start; }
    .hint-icon { color: #afb42b; font-size: 18px; flex-shrink: 0; margin-top: 2px; }
    .hint-texts p { margin: 0; font-size: 12px; color: #555; line-height: 1.5; }
    .legend { display: flex; flex-direction: column; gap: 4px; }
    .legend-item { display: flex; align-items: center; gap: 6px; font-size: 12px; }
    .ldot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
    .shortcuts { display: flex; flex-direction: column; gap: 3px; }
    .shortcuts div { font-size: 11px; color: #666; }
    kbd {
      background: #f0f0f0; border: 1px solid #ccc; border-radius: 3px;
      padding: 1px 4px; font-size: 10px; font-family: monospace;
    }

    .loading-overlay {
      position: absolute; inset: 0; background: rgba(255,255,255,0.75);
      display: flex; align-items: center; justify-content: center; z-index: 10;
    }
    .no-bld {
      display: flex; flex-direction: column; align-items: center;
      justify-content: center; gap: 16px; padding: 64px; color: #9e9e9e; font-size: 16px;
    }
    .no-bld mat-icon { font-size: 64px; height: 64px; width: 64px; }
  `]
})
export class FloorPlanEditorComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('svgCanvas')    svgCanvas!:    ElementRef<SVGSVGElement>;
  @ViewChild('canvasWrapper') canvasWrapper!: ElementRef<HTMLDivElement>;

  buildings:         Building[] = [];
  selectedBuildingId: string | null = null;
  selectedBuilding:   Building | null = null;
  buildingRooms:      Room[] = [];

  nodes:        EditorNode[] = [];
  edges:        FloorPlanEdge[] = [];
  floors:       number[] = [];
  currentFloor = 1;
  scale        = 5;
  dirty        = false;
  loading      = false;
  saving       = false;

  mode:       EditorMode        = 'select';
  placeType:  FloorPlanNodeType = FloorPlanNodeType.Room;
  edgeSource: { nodeId: string } | null = null;
  selectedEdgeId: string | null = null;
  mousePos:   { x: number; y: number } | null = null;

  // ViewBox
  vx = 0; vy = 0; vw = 900; vh = 650;

  // Drag
  private dragging:   EditorNode | null = null;
  private dragOffset = { x: 0, y: 0 };
  private dragMoved  = false;

  // Pan
  private panning   = false;
  private panAnchor = { sx: 0, sy: 0, vx: 0, vy: 0 };

  private wheelHandler = (e: WheelEvent) => {
    e.preventDefault();
    if (e.ctrlKey || e.metaKey) {
      const factor = e.deltaY > 0 ? 1.12 : 0.89;
      const { x, y } = this.svgPoint(e as unknown as MouseEvent);
      const nw = this.vw * factor, nh = this.vh * factor;
      this.vx = x - (x - this.vx) * (nw / this.vw);
      this.vy = y - (y - this.vy) * (nh / this.vh);
      this.vw = nw; this.vh = nh;
    } else {
      const r = this.svgCanvas?.nativeElement?.getBoundingClientRect();
      const rx = r ? this.vw / r.width  : 1;
      const ry = r ? this.vh / r.height : 1;
      this.vx += e.deltaX * rx;
      this.vy += e.deltaY * ry;
    }
  };

  readonly NR             = NODE_RADIUS;
  readonly FNT            = FloorPlanNodeType;
  readonly FloorPlanNodeType = FloorPlanNodeType;

  readonly nodeTypes = [
    { type: FloorPlanNodeType.Room,      label: 'Аудитория', color: NODE_COLORS[FloorPlanNodeType.Room]      },
    { type: FloorPlanNodeType.Staircase, label: 'Лестница',  color: NODE_COLORS[FloorPlanNodeType.Staircase] },
    { type: FloorPlanNodeType.Elevator,  label: 'Лифт',      color: NODE_COLORS[FloorPlanNodeType.Elevator]  },
    { type: FloorPlanNodeType.Entrance,  label: 'Вход',      color: NODE_COLORS[FloorPlanNodeType.Entrance]  },
    { type: FloorPlanNodeType.Corridor,  label: 'Коридор',   color: NODE_COLORS[FloorPlanNodeType.Corridor]  },
  ];

  get selectedNode(): EditorNode | undefined { return this.nodes.find(n => n.selected); }
  get viewBox():      string { return `${this.vx} ${this.vy} ${this.vw} ${this.vh}`; }
  get placeTypeLabel(): string { return this.nodeTypes.find(t => t.type === this.placeType)?.label ?? ''; }
  get cursor(): string {
    if (this.panning)         return 'grabbing';
    if (this.mode === 'place') return 'crosshair';
    if (this.mode === 'delete') return 'not-allowed';
    if (this.mode === 'edge')   return 'cell';
    return 'default';
  }

  constructor(private api: ApiService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getBuildings().subscribe(bs => { this.buildings = bs; });
  }

  ngAfterViewInit(): void {
    this.svgCanvas?.nativeElement?.addEventListener('wheel', this.wheelHandler, { passive: false });
  }

  ngOnDestroy(): void {
    this.svgCanvas?.nativeElement?.removeEventListener('wheel', this.wheelHandler);
  }

  @HostListener('window:keydown', ['$event'])
  onKeyDown(e: KeyboardEvent): void {
    const tag = (document.activeElement as HTMLElement)?.tagName ?? '';
    if (['INPUT', 'TEXTAREA'].includes(tag)) return;
    if (e.key === 'Delete' || e.key === 'Backspace') {
      const n = this.selectedNode;
      if (n) { this.deleteNode(n); return; }
      const ed = this.selectedEdge();
      if (ed) this.deleteEdge(ed);
    }
    if (e.key === 'Escape') {
      if (this.edgeSource) { this.edgeSource = null; return; }
      if (this.mode !== 'select') { this.mode = 'select'; return; }
      this.clearSelection();
    }
    if (e.ctrlKey && e.key === 's') { e.preventDefault(); this.save(); }
  }

  @HostListener('window:blur')
  onBlur(): void { this.panning = false; this.dragging = null; }

  // ── Building ─────────────────────────────────────────────────────────────────

  onBuildingChange(id: string): void {
    this.selectedBuilding = this.buildings.find(b => b.id === id) ?? null;
    this.buildFloors();
    this.api.getRooms({ buildingId: id }).subscribe(r => { this.buildingRooms = r; });

    const draft = localStorage.getItem(`fp_draft_${id}`);
    if (draft) {
      this.snackBar.open('Обнаружен несохранённый черновик', 'Восстановить', { duration: 8000 })
        .onAction().subscribe(() => this.restoreDraft(id));
    }
    this.loadFloorPlan(id);
  }

  buildFloors(): void {
    if (!this.selectedBuilding) { this.floors = []; return; }
    const b = this.selectedBuilding;
    const res: number[] = [];
    for (let f = -b.numberOfBasementFloors; f <= b.numberOfFloors; f++) {
      if (f !== 0) res.push(f);
    }
    this.floors = res;
    this.currentFloor = res.includes(1) ? 1 : (res[0] ?? 1);
  }

  loadFloorPlan(id: string): void {
    this.loading = true;
    this.nodes = []; this.edges = [];
    this.clearSelection();
    this.api.getFloorPlan(id).subscribe({
      next: fp => {
        this.nodes = fp.nodes.map(n => ({ ...n, selected: false }));
        this.edges = fp.edges;
        this.dirty = false;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  reload(): void {
    if (!this.selectedBuildingId) return;
    localStorage.removeItem(`fp_draft_${this.selectedBuildingId}`);
    this.loadFloorPlan(this.selectedBuildingId);
  }

  restoreDraft(id: string): void {
    const raw = localStorage.getItem(`fp_draft_${id}`);
    if (!raw) return;
    try {
      const d = JSON.parse(raw);
      this.nodes = (d.nodes as EditorNode[]).map(n => ({ ...n, selected: false }));
      this.edges = d.edges;
      this.scale = d.scale ?? 5;
      this.dirty = true;
      this.snackBar.open('Черновик восстановлен', '', { duration: 2000 });
    } catch { localStorage.removeItem(`fp_draft_${id}`); }
  }

  private saveDraft(): void {
    if (!this.selectedBuildingId) return;
    localStorage.setItem(`fp_draft_${this.selectedBuildingId}`,
      JSON.stringify({ nodes: this.nodes, edges: this.edges, scale: this.scale }));
  }

  // ── Floor / view ─────────────────────────────────────────────────────────────

  selectFloor(f: number): void { this.currentFloor = f; this.clearSelection(); }
  floorLabel(f: number): string { return f < 0 ? `Подвал ${Math.abs(f)}` : `Этаж ${f}`; }

  currentFloorNodes(): EditorNode[] { return this.nodes.filter(n => n.floor === this.currentFloor); }
  currentFloorEdges(): FloorPlanEdge[] {
    const ids = new Set(this.currentFloorNodes().map(n => n.id));
    return this.edges.filter(e => ids.has(e.fromNodeId) || ids.has(e.toNodeId));
  }

  zoom(dir: 1 | -1): void {
    const f  = dir > 0 ? 0.8 : 1.25;
    const cx = this.vx + this.vw / 2, cy = this.vy + this.vh / 2;
    this.vw *= f; this.vh *= f;
    this.vx = cx - this.vw / 2; this.vy = cy - this.vh / 2;
  }
  resetView(): void { this.vx = 0; this.vy = 0; this.vw = 900; this.vh = 650; }

  // ── Node helpers ─────────────────────────────────────────────────────────────

  nodeById(id: string): EditorNode | undefined { return this.nodes.find(n => n.id === id); }
  nodeColor(n: FloorPlanNode): string  { return NODE_COLORS[n.nodeType] ?? '#999'; }
  nodeIcon(n: FloorPlanNode): string   { return NODE_ICONS[n.nodeType]  ?? '?'; }

  nodeDisplayLabel(n: EditorNode): string {
    if (n.label) return n.label;
    if (n.nodeType === FloorPlanNodeType.Room)
      return this.buildingRooms.find(r => r.id === n.roomId)?.number ?? '';
    return '';
  }

  isUnlinkedRoom(n: EditorNode): boolean {
    return n.nodeType === FloorPlanNodeType.Room && !n.roomId && !n.label;
  }

  isEdgeCrossFloor(e: FloorPlanEdge): boolean {
    const a = this.nodeById(e.fromNodeId), b = this.nodeById(e.toNodeId);
    return !!a && !!b && a.floor !== b.floor;
  }

  hasCrossFloorEdge(n: FloorPlanNode): boolean {
    return this.edges.some(e => {
      if (e.fromNodeId !== n.id && e.toNodeId !== n.id) return false;
      const other = this.nodeById(e.fromNodeId === n.id ? e.toNodeId : e.fromNodeId);
      return !!other && other.floor !== n.floor;
    });
  }

  edgeMidX(e: FloorPlanEdge): number { return ((this.nodeById(e.fromNodeId)?.x ?? 0) + (this.nodeById(e.toNodeId)?.x ?? 0)) / 2; }
  edgeMidY(e: FloorPlanEdge): number { return ((this.nodeById(e.fromNodeId)?.y ?? 0) + (this.nodeById(e.toNodeId)?.y ?? 0)) / 2; }
  selectedEdge(): FloorPlanEdge | undefined { return this.edges.find(e => e.id === this.selectedEdgeId); }

  // ── Mode / selection ─────────────────────────────────────────────────────────

  setMode(m: EditorMode): void { this.mode = m; this.edgeSource = null; if (m !== 'select') this.clearSelection(); }
  setPlaceType(t: FloorPlanNodeType): void { this.placeType = t; this.mode = 'place'; }

  clearSelection(): void {
    this.nodes.forEach(n => n.selected = false);
    this.selectedEdgeId = null;
    this.edgeSource = null;
  }

  markDirty(): void { this.dirty = true; this.saveDraft(); }

  onNodeTypeChange(node: EditorNode): void {
    if (node.nodeType !== FloorPlanNodeType.Room) node.roomId = null;
    this.markDirty();
  }

  onRoomAssigned(node: EditorNode, roomId: string | null): void {
    node.roomId = roomId;
    if (roomId) {
      const r = this.buildingRooms.find(r => r.id === roomId);
      if (r && !node.label) node.floor = r.floor;
    }
    this.markDirty();
  }

  // ── Staircase multi-floor ────────────────────────────────────────────────────

  isMultiFloorType(n: EditorNode): boolean {
    return n.nodeType === FloorPlanNodeType.Staircase || n.nodeType === FloorPlanNodeType.Elevator;
  }

  private stairGroup(ref: EditorNode): EditorNode[] {
    return this.nodes.filter(n =>
      n.nodeType === ref.nodeType &&
      Math.abs(n.x - ref.x) < 15 && Math.abs(n.y - ref.y) < 15
    );
  }

  isStairOnFloor(ref: EditorNode, floor: number): boolean {
    return this.stairGroup(ref).some(n => n.floor === floor);
  }

  toggleStairFloor(ref: EditorNode, floor: number, checked: boolean): void {
    if (checked) {
      if (this.isStairOnFloor(ref, floor)) return;
      this.nodes.push({
        id: crypto.randomUUID(), buildingId: this.selectedBuildingId!,
        floor, x: ref.x, y: ref.y, nodeType: ref.nodeType,
        roomId: null, label: ref.label, selected: false
      });
    } else {
      if (ref.floor === floor) return;
      const victim = this.stairGroup(ref).find(n => n.floor === floor && n.id !== ref.id);
      if (victim) {
        this.edges = this.edges.filter(e => e.fromNodeId !== victim.id && e.toNodeId !== victim.id);
        this.nodes = this.nodes.filter(n => n.id !== victim.id);
      }
    }
    this.reconnectStairGroup(ref);
    this.markDirty();
  }

  extendToAllFloors(ref: EditorNode): void {
    const existing = new Set(this.stairGroup(ref).map(n => n.floor));
    for (const f of this.floors) {
      if (!existing.has(f)) {
        this.nodes.push({
          id: crypto.randomUUID(), buildingId: this.selectedBuildingId!,
          floor: f, x: ref.x, y: ref.y, nodeType: ref.nodeType,
          roomId: null, label: ref.label, selected: false
        });
      }
    }
    this.reconnectStairGroup(ref);
    this.markDirty();
  }

  private reconnectStairGroup(ref: EditorNode): void {
    const group   = this.stairGroup(ref).sort((a, b) => a.floor - b.floor);
    const gids    = new Set(group.map(n => n.id));
    this.edges    = this.edges.filter(e => !(gids.has(e.fromNodeId) && gids.has(e.toNodeId)));
    for (let i = 0; i < group.length - 1; i++) {
      this.edges.push({
        id: crypto.randomUUID(), buildingId: this.selectedBuildingId!,
        fromNodeId: group[i].id, toNodeId: group[i + 1].id, distanceMeters: 15
      });
    }
  }

  // ── Mouse events ─────────────────────────────────────────────────────────────

  onCanvasMouseDown(event: MouseEvent): void {
    if (event.button === 1 || event.altKey) { event.preventDefault(); this.startPan(event); return; }
    if (event.target !== event.currentTarget) return;

    const { x, y } = this.svgPoint(event);
    if (this.mode === 'place') {
      this.placeNode(x, y);
    } else if (this.mode === 'select') {
      this.clearSelection();
      this.startPan(event);
    } else if (this.mode === 'delete') {
      this.clearSelection();
    }
  }

  private startPan(event: MouseEvent): void {
    this.panning   = true;
    this.panAnchor = { sx: event.clientX, sy: event.clientY, vx: this.vx, vy: this.vy };
  }

  onNodeMouseDown(event: MouseEvent, node: EditorNode): void {
    event.stopPropagation();
    if (event.button === 1 || event.altKey) { this.startPan(event); return; }

    const { x, y } = this.svgPoint(event);

    if (this.mode === 'delete') { this.deleteNode(node); return; }

    if (this.mode === 'edge') {
      if (!this.edgeSource) {
        this.edgeSource = { nodeId: node.id };
      } else {
        if (this.edgeSource.nodeId !== node.id) this.createEdge(this.edgeSource.nodeId, node.id);
        this.edgeSource = null;
      }
      return;
    }

    // select or place mode: select & possibly drag
    this.clearSelection();
    node.selected    = true;
    this.selectedEdgeId = null;
    this.dragging    = node;
    this.dragOffset  = { x: x - node.x, y: y - node.y };
    this.dragMoved   = false;
  }

  onEdgeMouseDown(event: MouseEvent, edge: FloorPlanEdge): void {
    event.stopPropagation();
    if (this.mode === 'delete') { this.deleteEdge(edge); return; }
    if (this.mode === 'select') {
      this.clearSelection();
      this.selectedEdgeId = edge.id;
    }
  }

  onMouseMove(event: MouseEvent): void {
    if (this.panning) {
      const r = this.svgCanvas?.nativeElement?.getBoundingClientRect();
      if (r) {
        this.vx = this.panAnchor.vx - (event.clientX - this.panAnchor.sx) * (this.vw / r.width);
        this.vy = this.panAnchor.vy - (event.clientY - this.panAnchor.sy) * (this.vh / r.height);
      }
      return;
    }

    const { x, y } = this.svgPoint(event);
    this.mousePos = { x, y };

    if (this.dragging) {
      this.dragging.x = x - this.dragOffset.x;
      this.dragging.y = y - this.dragOffset.y;
      this.dirty     = true;
      this.dragMoved = true;
    }
  }

  onMouseUp(_event: MouseEvent): void {
    if (this.panning)  { this.panning = false; return; }
    if (this.dragging && this.dragMoved) this.saveDraft();
    this.dragging  = null;
    this.dragMoved = false;
  }

  onMouseLeave(event: MouseEvent): void {
    this.onMouseUp(event);
    this.mousePos = null;
  }

  // ── Operations ───────────────────────────────────────────────────────────────

  placeNode(x: number, y: number): void {
    const node: EditorNode = {
      id: crypto.randomUUID(), buildingId: this.selectedBuildingId!,
      floor: this.currentFloor, x, y,
      nodeType: this.placeType, roomId: null, label: null, selected: true
    };
    this.nodes.forEach(n => n.selected = false);
    this.nodes.push(node);
    this.mode = 'select';
    this.markDirty();
  }

  createEdge(fromId: string, toId: string): void {
    if (this.edges.some(e =>
      (e.fromNodeId === fromId && e.toNodeId === toId) ||
      (e.fromNodeId === toId   && e.toNodeId === fromId))) return;

    const a = this.nodeById(fromId)!, b = this.nodeById(toId)!;
    const dx = a.x - b.x, dy = a.y - b.y;
    this.edges.push({
      id: crypto.randomUUID(), buildingId: this.selectedBuildingId!,
      fromNodeId: fromId, toNodeId: toId,
      distanceMeters: Math.max(1, Math.round(Math.sqrt(dx*dx + dy*dy) * this.scale / 100))
    });
    this.markDirty();
  }

  deleteNode(node: EditorNode): void {
    this.edges = this.edges.filter(e => e.fromNodeId !== node.id && e.toNodeId !== node.id);
    this.nodes = this.nodes.filter(n => n.id !== node.id);
    this.markDirty();
  }

  deleteEdge(edge: FloorPlanEdge): void {
    this.edges = this.edges.filter(e => e.id !== edge.id);
    if (this.selectedEdgeId === edge.id) this.selectedEdgeId = null;
    this.markDirty();
  }

  save(): void {
    if (!this.selectedBuildingId || this.saving) return;
    this.saving = true;
    const req = {
      nodes: this.nodes.map(n => ({
        id: n.id, floor: n.floor, x: n.x, y: n.y,
        nodeType: n.nodeType, roomId: n.roomId, label: n.label
      })),
      edges: this.edges.map(e => ({
        fromNodeId: e.fromNodeId, toNodeId: e.toNodeId, distanceMeters: e.distanceMeters
      }))
    };
    this.api.saveFloorPlan(this.selectedBuildingId, req).subscribe({
      next: () => {
        this.saving = false; this.dirty = false;
        localStorage.removeItem(`fp_draft_${this.selectedBuildingId}`);
        const unlinked = this.nodes.filter(n => this.isUnlinkedRoom(n)).length;
        this.snackBar.open(
          unlinked > 0 ? `Сохранено (${unlinked} ауд. без привязки)` : 'Планировка сохранена',
          'OK', { duration: 2500 }
        );
        this.loadFloorPlan(this.selectedBuildingId!);
      },
      error: err => {
        this.saving = false;
        this.snackBar.open(err.error?.title || 'Ошибка сохранения', 'OK', { duration: 4000 });
      }
    });
  }

  // ── SVG coord helper ─────────────────────────────────────────────────────────

  private svgPoint(event: MouseEvent): { x: number; y: number } {
    const el = this.svgCanvas?.nativeElement;
    if (!el) return { x: 0, y: 0 };
    const r = el.getBoundingClientRect();
    return {
      x: this.vx + (event.clientX - r.left) * (this.vw / r.width),
      y: this.vy + (event.clientY - r.top)  * (this.vh / r.height),
    };
  }
}
