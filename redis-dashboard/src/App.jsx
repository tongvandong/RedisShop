import { useCallback, useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import {
  API_BASE_URL,
  checkPersistenceTest,
  clearPersistenceTest,
  clearProductsCache,
  getRedisDetails,
  getRedisInfrastructure,
  getRedisOrderStream,
  getRedisOverview,
  getRedisProductViewRanking,
  getRedisRanking,
  preparePersistenceTest,
  publishRedisMessage,
} from './api/client.js'
import { Layout } from './components/Layout.jsx'
import { InfrastructurePage } from './pages/InfrastructurePage.jsx'
import { OverviewPage } from './pages/OverviewPage.jsx'
import { StreamsPage } from './pages/StreamsPage.jsx'
import './App.css'

const AUTO_REFRESH_SECONDS = 1

function App() {
  const [overview, setOverview] = useState(null)
  const [details, setDetails] = useState(null)
  const [infrastructure, setInfrastructure] = useState(null)
  const [ranking, setRanking] = useState([])
  const [viewRanking, setViewRanking] = useState([])
  const [streamMessages, setStreamMessages] = useState([])
  const [persistenceTest, setPersistenceTest] = useState(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [lastUpdated, setLastUpdated] = useState('')
  const [lastNotification, setLastNotification] = useState({
    channel: 'notifications',
    message: 'Chưa nhận thông báo nào trong phiên này.',
    time: '--:--:--',
  })
  const [notifications, setNotifications] = useState([])
  const [sseStatus, setSseStatus] = useState('Đang kết nối SSE...')
  const [publishResult, setPublishResult] = useState(null)

  const refreshAll = useCallback(async (silent = false) => {
    if (!silent) {
      setIsLoading(true)
    }
    setError('')

    try {
      const results = await Promise.allSettled([
        getRedisOverview(),
        getRedisDetails(),
        getRedisInfrastructure(),
        getRedisRanking(),
        getRedisProductViewRanking(),
        getRedisOrderStream(),
        checkPersistenceTest(),
      ])

      if (results[0].status === 'fulfilled') setOverview(results[0].value)
      if (results[1].status === 'fulfilled') setDetails(results[1].value)
      if (results[2].status === 'fulfilled') setInfrastructure(results[2].value)
      if (results[3].status === 'fulfilled') setRanking(results[3].value)
      if (results[4].status === 'fulfilled') setViewRanking(results[4].value)
      if (results[5].status === 'fulfilled') setStreamMessages(results[5].value)
      if (results[6].status === 'fulfilled') setPersistenceTest(results[6].value)

      const failed = results.find((result) => result.status === 'rejected')
      if (failed) {
        setError(failed.reason?.message ?? 'Một phần dữ liệu dashboard chưa cập nhật được.')
      } else {
        setError('')
      }

      setLastUpdated(new Date().toLocaleTimeString('vi-VN'))
    } catch (exception) {
      setError(exception.message)
    } finally {
      if (!silent) {
        setIsLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    refreshAll()
    const timer = window.setInterval(() => {
      refreshAll(true)
    }, AUTO_REFRESH_SECONDS * 1000)

    return () => window.clearInterval(timer)
  }, [refreshAll])

  useEffect(() => {
    const events = new EventSource(`${API_BASE_URL}/api/redis/pubsub/notifications`)

    events.addEventListener('ready', (event) => {
      const payload = JSON.parse(event.data)
      setSseStatus(`SSE connected · ${payload.sseClients ?? 1} client`)
      setError('')
    })

    events.addEventListener('notification', (event) => {
      const payload = JSON.parse(event.data)
      const notification = {
        channel: payload.channel,
        message: payload.message,
        time: payload.time,
        receivedBy: payload.receivedBy,
      }
      setLastNotification(notification)
      setNotifications((current) => [notification, ...current].slice(0, 8))
      refreshAll(true)
    })

    events.onerror = () => {
      setSseStatus('SSE disconnected')
      setError('Mất kết nối SSE tới kênh Pub/Sub notifications.')
    }

    return () => events.close()
  }, [refreshAll])

  async function refreshCacheMetrics() {
    setIsLoading(true)
    setError('')

    try {
      const [nextOverview, nextDetails] = await Promise.all([
        getRedisOverview(),
        getRedisDetails(),
      ])
      setOverview(nextOverview)
      setDetails(nextDetails)
      setLastUpdated(new Date().toLocaleTimeString('vi-VN'))
    } catch (exception) {
      setError(exception.message)
    } finally {
      setIsLoading(false)
    }
  }

  async function clearCache() {
    setError('')

    try {
      await clearProductsCache()
      await refreshCacheMetrics()
    } catch (exception) {
      setError(exception.message)
    }
  }

  async function publishDemoMessage(message) {
    setError('')

    try {
      const result = await publishRedisMessage('notifications', message)
      setPublishResult(result)
    } catch (exception) {
      setError(exception.message)
    }
  }

  async function preparePersistence() {
    setError('')

    try {
      const result = await preparePersistenceTest()
      setPersistenceTest(result)
      await refreshAll(true)
    } catch (exception) {
      setError(exception.message)
    }
  }

  async function checkPersistence() {
    setError('')

    try {
      const result = await checkPersistenceTest()
      setPersistenceTest(result)
    } catch (exception) {
      setError(exception.message)
    }
  }

  async function clearPersistence() {
    setError('')

    try {
      await clearPersistenceTest()
      const result = await checkPersistenceTest()
      setPersistenceTest(result)
      await refreshAll(true)
    } catch (exception) {
      setError(exception.message)
    }
  }

  return (
    <Layout
      online={overview?.online}
      endpoint={overview?.endpoint}
      lastUpdated={lastUpdated}
      refreshSeconds={AUTO_REFRESH_SECONDS}
      onRefresh={() => refreshAll()}
    >
      <Routes>
        <Route path="/" element={<Navigate to="/redis/overview" replace />} />
        <Route path="/redis" element={<Navigate to="/redis/overview" replace />} />
        <Route
          path="/redis/overview"
          element={
            <OverviewPage
              overview={overview}
              details={details}
              ranking={ranking}
              viewRanking={viewRanking}
              isLoading={isLoading}
              error={error}
              lastNotification={lastNotification}
              notifications={notifications}
              sseStatus={sseStatus}
              publishResult={publishResult}
              onRefreshProducts={refreshCacheMetrics}
              onClearCache={clearCache}
              onPublish={publishDemoMessage}
            />
          }
        />
        <Route
          path="/redis/streams"
          element={<StreamsPage overview={overview} details={details} messages={streamMessages} />}
        />
        <Route
          path="/redis/infrastructure"
          element={
            <InfrastructurePage
              overview={overview}
              infrastructure={infrastructure}
              persistenceTest={persistenceTest}
              onPreparePersistenceTest={preparePersistence}
              onCheckPersistenceTest={checkPersistence}
              onClearPersistenceTest={clearPersistence}
            />
          }
        />
        <Route path="*" element={<Navigate to="/redis/overview" replace />} />
      </Routes>
    </Layout>
  )
}

export default App
