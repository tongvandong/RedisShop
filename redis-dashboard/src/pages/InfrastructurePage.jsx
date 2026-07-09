import { Fragment, useEffect, useMemo, useState } from 'react'
import { Activity, CheckCircle2, DatabaseZap, HardDrive, Network, PlayCircle, RefreshCcw, Server, ShieldCheck, Trash2 } from 'lucide-react'
import { MetricCard, Panel, StatusPill } from '../components/Ui.jsx'

function aofLabel(value) {
  return value === '1' ? 'Enabled' : 'Disabled'
}

function done(value) {
  return value ? 'Hoàn tất' : 'Cần kiểm tra'
}

function statusTone(status) {
  if (status === 'Current master' || status === 'Online') {
    return 'green'
  }

  if (status === 'Degraded') {
    return 'amber'
  }

  return 'red'
}

function ttlLabel(value) {
  if (value === -2) {
    return 'Không tồn tại'
  }

  if (value === -1) {
    return 'Không hết hạn'
  }

  return `${value ?? 0}s`
}

export function InfrastructurePage({
  overview,
  infrastructure,
  persistenceTest,
  onPreparePersistenceTest,
  onCheckPersistenceTest,
  onClearPersistenceTest,
}) {
  const currentMaster = infrastructure?.endpoint ?? overview?.endpoint ?? '--'
  const role = infrastructure?.role || '--'
  const replicaCount = Number(infrastructure?.connectedReplicas ?? 0)
  const topologyNodes = infrastructure?.nodes ?? []
  const usesSentinel = infrastructure?.connectionMode === 'Sentinel'
  const rdbOk = infrastructure?.persistenceRdb === 'ok'
  const aofEnabled = infrastructure?.persistenceAof === '1'
  const healthyNodes = topologyNodes.filter((node) => node.status === 'Current master' || node.status === 'Online').length
  const haReady = usesSentinel && role === 'master' && replicaCount > 0
  const [selectedEndpoint, setSelectedEndpoint] = useState('')
  const [persistenceAction, setPersistenceAction] = useState('')
  const selectedNode = useMemo(() => {
    return topologyNodes.find((node) => node.endpoint === selectedEndpoint)
      ?? topologyNodes.find((node) => node.endpoint === currentMaster)
      ?? topologyNodes[0]
      ?? null
  }, [currentMaster, selectedEndpoint, topologyNodes])

  useEffect(() => {
    if (!topologyNodes.length) {
      return
    }

    const stillExists = topologyNodes.some((node) => node.endpoint === selectedEndpoint)
    if (!selectedEndpoint || !stillExists) {
      setSelectedEndpoint(currentMaster !== '--' ? currentMaster : topologyNodes[0].endpoint)
    }
  }, [currentMaster, selectedEndpoint, topologyNodes])

  async function runPersistenceAction(action, handler) {
    setPersistenceAction(action)
    try {
      await handler()
    } finally {
      setPersistenceAction('')
    }
  }

  return (
    <section className="page-stack">
      <div className="metric-grid">
        <MetricCard icon={Network} label="Current master" value={currentMaster} sublabel="Master do Sentinel trả về" tone="blue" />
        <MetricCard icon={Server} label="Replication" value={`${replicaCount} replica`} sublabel="Số replica đang kết nối master" tone={replicaCount > 0 ? 'green' : 'amber'} />
        <MetricCard icon={ShieldCheck} label="Sentinel" value={usesSentinel ? 'Active' : 'Inactive'} sublabel={infrastructure?.configuredEndpoint || '--'} tone={usesSentinel ? 'green' : 'red'} />
        <MetricCard icon={HardDrive} label="Cluster health" value={`${healthyNodes}/${topologyNodes.length || 3} node`} sublabel="Trạng thái đọc trực tiếp từ Redis node" tone={haReady ? 'green' : 'amber'} />
      </div>

      <Panel icon={Activity} title="Redis HA Topology" description="Trạng thái thực tế của 3 Redis node và 3 Sentinel trong cụm.">
        <div className="topology">
          {topologyNodes.map((node, index) => (
            <Fragment key={node.name}>
              <div className={node.role === 'Master' ? 'node-card active-node' : 'node-card'}>
                <strong>{node.name}</strong>
                <span>{node.endpoint}</span>
                <StatusPill tone={node.role === 'Master' ? 'green' : statusTone(node.status)} label={node.role} />
              </div>
              {index < topologyNodes.length - 1 && <div className="topology-line" />}
            </Fragment>
          ))}
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Node</th>
                <th>Redis endpoint</th>
                <th>Sentinel endpoint</th>
                <th>Memory policy</th>
                <th>Vai trò</th>
                <th>Trạng thái</th>
                <th>Ghi chú</th>
              </tr>
            </thead>
            <tbody>
              {topologyNodes.map((node) => (
                <tr key={node.endpoint}>
                  <td>{node.name}</td>
                  <td className="mono-cell">{node.endpoint}</td>
                  <td className="mono-cell">{node.sentinelEndpoint}</td>
                  <td className="mono-cell">{node.maxMemoryPolicy || '--'}</td>
                  <td>{node.role}</td>
                  <td>
                    <StatusPill tone={statusTone(node.status)} label={node.status} />
                  </td>
                  <td>{node.note}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>

      <div className="content-split equal-split">
        <Panel
          icon={DatabaseZap}
          title="Redis Node INFO"
          description="Chọn một Redis node để xem thông số INFO đọc trực tiếp từ node đó."
        >
          <div className="node-info-toolbar">
            <label htmlFor="redis-node-select">Redis node</label>
            <select
              id="redis-node-select"
              value={selectedNode?.endpoint || ''}
              onChange={(event) => setSelectedEndpoint(event.target.value)}
            >
              {topologyNodes.map((node) => (
                <option key={node.endpoint} value={node.endpoint}>
                  {node.name} - {node.endpoint} - {node.role}
                </option>
              ))}
            </select>
          </div>
          <div className="info-grid compact-info">
            <div>
              <span>Connection</span>
              <strong>{infrastructure?.connectionMode || 'Direct'}</strong>
            </div>
            <div>
              <span>Role</span>
              <strong>{selectedNode?.role || role}</strong>
            </div>
            <div>
              <span>Redis version</span>
              <strong>{selectedNode?.redisVersion || infrastructure?.redisVersion || '--'}</strong>
            </div>
            <div>
              <span>Mode</span>
              <strong>{selectedNode?.mode || infrastructure?.mode || '--'}</strong>
            </div>
            <div>
              <span>RDB save</span>
              <strong>{selectedNode?.persistenceRdb || infrastructure?.persistenceRdb || '--'}</strong>
            </div>
            <div>
              <span>AOF</span>
              <strong>{aofLabel(selectedNode?.persistenceAof || infrastructure?.persistenceAof)}</strong>
            </div>
            <div>
              <span>Memory policy</span>
              <strong>{selectedNode?.maxMemoryPolicy || infrastructure?.maxMemoryPolicy || '--'}</strong>
            </div>
            <div>
              <span>Maxmemory</span>
              <strong>{selectedNode?.maxMemoryHuman || infrastructure?.maxMemoryHuman || '--'}</strong>
            </div>
            <div>
              <span>Memory peak</span>
              <strong>{selectedNode?.usedMemoryPeakHuman || infrastructure?.usedMemoryPeakHuman || overview?.usedMemoryHuman || '0B'}</strong>
            </div>
            <div>
              <span>OS</span>
              <strong>{selectedNode?.os || infrastructure?.os || '--'}</strong>
            </div>
          </div>
        </Panel>

        <Panel icon={CheckCircle2} title="HA Checklist" description="Các thành phần đang chạy trong cụm Redis HA.">
          <div className="checklist">
            <div className={usesSentinel ? 'done' : 'todo'}>
              <CheckCircle2 size={18} />
              <span>Sentinel discovery</span>
              <strong>{done(usesSentinel)}</strong>
            </div>
            <div className={role === 'master' ? 'done' : 'todo'}>
              <CheckCircle2 size={18} />
              <span>Writable master</span>
              <strong>{done(role === 'master')}</strong>
            </div>
            <div className={replicaCount > 0 ? 'done' : 'todo'}>
              <CheckCircle2 size={18} />
              <span>Replication</span>
              <strong>{replicaCount} replica</strong>
            </div>
            <div className={rdbOk ? 'done' : 'todo'}>
              <CheckCircle2 size={18} />
              <span>RDB persistence</span>
              <strong>{done(rdbOk)}</strong>
            </div>
            <div className={aofEnabled ? 'done' : 'todo'}>
              <CheckCircle2 size={18} />
              <span>AOF persistence</span>
              <strong>{aofEnabled ? 'Enabled' : 'Disabled'}</strong>
            </div>
          </div>
        </Panel>
      </div>

      <Panel icon={HardDrive} title="Redis instance tách riêng" description="Xác minh cấu hình thật của Redis Cache 6380 và Persistence Test 6381.">
        <div className="info-grid compact-info">
          <div>
            <span>Cache endpoint</span>
            <strong>{infrastructure?.cacheEndpoint || '--'}</strong>
          </div>
          <div>
            <span>Cache maxmemory</span>
            <strong>{infrastructure?.cacheMaxMemory || '--'}</strong>
          </div>
          <div>
            <span>Cache policy</span>
            <strong>{infrastructure?.cacheMaxMemoryPolicy || '--'}</strong>
          </div>
          <div>
            <span>Persistence endpoint</span>
            <strong>{infrastructure?.persistenceEndpoint || '--'}</strong>
          </div>
          <div>
            <span>AOF appendonly</span>
            <strong>{infrastructure?.persistenceAppendOnly || '--'}</strong>
          </div>
          <div>
            <span>RDB save</span>
            <strong>{infrastructure?.persistenceSave || '--'}</strong>
          </div>
        </div>
      </Panel>

      <Panel
        icon={HardDrive}
        title="Persistence Test"
        description="Tạo dữ liệu Redis thật trên instance 6381, restart Redis trên Ubuntu, rồi kiểm tra dữ liệu có được phục hồi hay không."
        action={
          <div className="button-row">
            <button
              type="button"
              className="primary-button"
              disabled={persistenceAction !== ''}
              onClick={() => runPersistenceAction('prepare', onPreparePersistenceTest)}
            >
              <PlayCircle size={16} />
              {persistenceAction === 'prepare' ? 'Creating...' : 'Create Persistence Test Data'}
            </button>
            <button
              type="button"
              className="secondary-button"
              disabled={persistenceAction !== ''}
              onClick={() => runPersistenceAction('check', onCheckPersistenceTest)}
            >
              <RefreshCcw size={16} />
              {persistenceAction === 'check' ? 'Checking...' : 'Check After Restart'}
            </button>
            <button
              type="button"
              className="danger-button"
              disabled={persistenceAction !== ''}
              onClick={() => runPersistenceAction('clear', onClearPersistenceTest)}
            >
              <Trash2 size={16} />
              {persistenceAction === 'clear' ? 'Clearing...' : 'Clear Test Data'}
            </button>
          </div>
        }
      >
        <div className="status-grid persistence-summary">
          <div>
            <span>Current keys</span>
            <strong>{persistenceTest?.totalKeys ?? 0}</strong>
          </div>
          <div>
            <span>Test keys</span>
            <strong>{persistenceTest ? `${persistenceTest.existingCount}/5 còn tồn tại` : 'Chưa kiểm tra'}</strong>
          </div>
          <div>
            <span>Missing</span>
            <strong>{persistenceTest?.missingCount ?? 0}</strong>
          </div>
          <div>
            <span>Result</span>
            <strong>
              <StatusPill
                tone={persistenceTest?.pass ? 'green' : persistenceTest ? 'red' : 'amber'}
                label={persistenceTest?.pass ? 'PASS' : persistenceTest ? 'FAIL' : 'PENDING'}
              />
            </strong>
          </div>
        </div>

        <div className="persistence-command">
          <strong>Lệnh restart trên Ubuntu</strong>
          <code>sudo systemctl restart redis-persistence-6381</code>
          <span>PASS nếu cả 5 key test vẫn tồn tại sau khi restart instance Redis 6381.</span>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Type</th>
                <th>TTL</th>
                <th>Tồn tại</th>
                <th>Giá trị</th>
              </tr>
            </thead>
            <tbody>
              {(persistenceTest?.keys ?? []).map((item) => (
                <tr key={item.key}>
                  <td className="mono-cell">{item.key}</td>
                  <td>{item.type}</td>
                  <td>{ttlLabel(item.ttl)}</td>
                  <td>
                    <StatusPill tone={item.exists ? 'green' : 'red'} label={item.exists ? 'Có' : 'Không'} />
                  </td>
                  <td>{item.valuePreview}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>
    </section>
  )
}


