import { useState, useCallback, useRef, useEffect } from 'react'
import axios from 'axios'
import {
  FileText,
  Upload,
  Download,
  CheckCircle,
  XCircle,
  Loader2,
  FolderDown,
  Trash2,
  RefreshCw,
  Moon,
  Sun,
  ArrowRight,
  FileType2,
  Wifi,
  WifiOff,
  Clock
} from 'lucide-react'
import './App.css'

// Backend status states
const BACKEND_STATUS = {
  CHECKING: 'checking',
  ONLINE: 'online',
  WAKING: 'waking',
  OFFLINE: 'offline'
}

function App() {
  const [files, setFiles] = useState([])
  const [converting, setConverting] = useState(false)
  const [results, setResults] = useState([])
  const [targetFormat, setTargetFormat] = useState('pdf')
  const [saveToDownloads, setSaveToDownloads] = useState(true)
  const [darkMode, setDarkMode] = useState(() => {
    return localStorage.getItem('theme') === 'dark'
  })
  const [dragActive, setDragActive] = useState(false)
  const [backendStatus, setBackendStatus] = useState(BACKEND_STATUS.CHECKING)
  const [wakeStartTime, setWakeStartTime] = useState(null)
  const [wakeElapsed, setWakeElapsed] = useState(0)
  const [conversionProgress, setConversionProgress] = useState({ current: 0, total: 0 })
  const fileInputRef = useRef(null)
  const healthCheckInterval = useRef(null)
  const wakeTimerInterval = useRef(null)

  // Check backend health
  const checkBackendHealth = useCallback(async (isInitial = false) => {
    try {
      const startTime = Date.now()
      const response = await axios.get('/api/health', { timeout: 10000 })
      const responseTime = Date.now() - startTime

      if (response.status === 200) {
        setBackendStatus(BACKEND_STATUS.ONLINE)
        setWakeStartTime(null)
        setWakeElapsed(0)
        // If we were waking and now online, clear the wake timer
        if (wakeTimerInterval.current) {
          clearInterval(wakeTimerInterval.current)
          wakeTimerInterval.current = null
        }
        return true
      }
    } catch (error) {
      // If initial check and we get a timeout or slow response, it's waking up
      if (error.code === 'ECONNABORTED' || error.response?.status === 503) {
        if (backendStatus !== BACKEND_STATUS.WAKING) {
          setBackendStatus(BACKEND_STATUS.WAKING)
          setWakeStartTime(Date.now())
        }
      } else if (error.message?.includes('Network Error') || !error.response) {
        // Network error could mean waking up on Render free tier
        if (isInitial || backendStatus === BACKEND_STATUS.CHECKING) {
          setBackendStatus(BACKEND_STATUS.WAKING)
          setWakeStartTime(Date.now())
        } else {
          setBackendStatus(BACKEND_STATUS.OFFLINE)
        }
      } else {
        setBackendStatus(BACKEND_STATUS.OFFLINE)
      }
    }
    return false
  }, [backendStatus])

  // Initial health check and periodic checks
  useEffect(() => {
    checkBackendHealth(true)

    // Check every 5 seconds when online, every 3 seconds when waking
    healthCheckInterval.current = setInterval(() => {
      checkBackendHealth(false)
    }, backendStatus === BACKEND_STATUS.WAKING ? 3000 : 10000)

    return () => {
      if (healthCheckInterval.current) {
        clearInterval(healthCheckInterval.current)
      }
    }
  }, [checkBackendHealth, backendStatus])

  // Wake timer
  useEffect(() => {
    if (backendStatus === BACKEND_STATUS.WAKING && wakeStartTime) {
      wakeTimerInterval.current = setInterval(() => {
        setWakeElapsed(Math.floor((Date.now() - wakeStartTime) / 1000))
      }, 1000)
    }

    return () => {
      if (wakeTimerInterval.current) {
        clearInterval(wakeTimerInterval.current)
      }
    }
  }, [backendStatus, wakeStartTime])

  // Toggle dark mode
  const toggleDarkMode = () => {
    const newMode = !darkMode
    setDarkMode(newMode)
    localStorage.setItem('theme', newMode ? 'dark' : 'light')
  }

  // Handle drag events
  const handleDrag = useCallback((e) => {
    e.preventDefault()
    e.stopPropagation()
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true)
    } else if (e.type === "dragleave") {
      setDragActive(false)
    }
  }, [])

  // Handle drop
  const handleDrop = useCallback((e) => {
    e.preventDefault()
    e.stopPropagation()
    setDragActive(false)

    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      const droppedFiles = Array.from(e.dataTransfer.files).filter(file =>
        isSupported(file.name)
      )
      setFiles(prev => [...prev, ...droppedFiles])
    }
  }, [])

  // Handle file input change
  const handleFileSelect = (e) => {
    if (e.target.files) {
      const selectedFiles = Array.from(e.target.files).filter(file =>
        isSupported(file.name)
      )
      setFiles(prev => [...prev, ...selectedFiles])
    }
  }

  // Check if file is supported
  const isSupported = (fileName) => {
    const ext = fileName.toLowerCase().split('.').pop()
    return ['pdf', 'docx', 'doc', 'odt', 'md', 'markdown'].includes(ext)
  }

  // Get file type class
  const getFileType = (fileName) => {
    const ext = fileName.toLowerCase().split('.').pop()
    if (ext === 'pdf') return 'pdf'
    if (['docx', 'doc'].includes(ext)) return 'word'
    if (['md', 'markdown'].includes(ext)) return 'markdown'
    if (ext === 'odt') return 'odt'
    return 'word'
  }

  // Get target format based on source
  const getTargetFormat = (fileName) => {
    const ext = fileName.toLowerCase().split('.').pop()
    if (['pdf', 'docx', 'doc', 'odt'].includes(ext)) {
      return 'md'
    }
    return targetFormat
  }

  // Format file size
  const formatSize = (bytes) => {
    if (bytes < 1024) return bytes + ' B'
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
  }

  // Remove file from list
  const removeFile = (index) => {
    setFiles(prev => prev.filter((_, i) => i !== index))
  }

  // Clear all files
  const clearFiles = () => {
    setFiles([])
    setResults([])
  }

  // Convert files
  const convertFiles = async () => {
    if (files.length === 0) return
    // Allow conversion attempt even if backend status is uncertain
    // The actual API call will fail if backend is truly down

    setConverting(true)
    setResults([])
    setConversionProgress({ current: 0, total: files.length })

    const formData = new FormData()
    files.forEach(file => {
      formData.append('files', file)
    })

    try {
      const response = await axios.post('/api/conversion/convert-batch', formData, {
        params: {
          targetFormat: targetFormat,
          saveToDownloads: saveToDownloads
        },
        headers: {
          'Content-Type': 'multipart/form-data'
        },
        timeout: 300000, // 5 minutes timeout for large files
        onUploadProgress: (progressEvent) => {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total)
          setConversionProgress(prev => ({ ...prev, uploadProgress: progress }))
        }
      })

      setResults(response.data.results)
      setConversionProgress({ current: files.length, total: files.length })
    } catch (error) {
      console.error('Conversion failed:', error)
      let errorMessage = 'Conversion failed'
      if (error.code === 'ECONNABORTED') {
        errorMessage = 'Request timed out. The file may be too large or the server is busy.'
      } else if (error.response?.data?.error) {
        errorMessage = error.response.data.error
      } else if (error.message) {
        errorMessage = error.message
      }
      setResults([{
        success: false,
        error: errorMessage
      }])
    } finally {
      setConverting(false)
      setConversionProgress({ current: 0, total: 0 })
    }
  }

  // Convert single file and download
  const convertSingleFile = async (file) => {
    // Allow conversion attempt regardless of backend status
    const formData = new FormData()
    formData.append('file', file)

    try {
      const response = await axios.post('/api/conversion/convert', formData, {
        params: {
          targetFormat: getTargetFormat(file.name),
          saveToDownloads: true
        },
        responseType: 'blob',
        headers: {
          'Content-Type': 'multipart/form-data'
        },
        timeout: 120000 // 2 minutes timeout
      })

      // Create download link
      const url = window.URL.createObjectURL(new Blob([response.data]))
      const link = document.createElement('a')
      const contentDisposition = response.headers['content-disposition']
      let fileName = file.name.replace(/\.[^/.]+$/, '') + '.md'

      if (contentDisposition) {
        const fileNameMatch = contentDisposition.match(/filename="?(.+)"?/i)
        if (fileNameMatch) {
          fileName = fileNameMatch[1]
        }
      }

      link.href = url
      link.setAttribute('download', fileName)
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(url)
    } catch (error) {
      console.error('Download failed:', error)
      let errorMessage = error.response?.data?.error || error.message || 'Download failed'
      if (error.code === 'ECONNABORTED') {
        errorMessage = 'Request timed out. Please try again.'
      }
      alert('Download failed: ' + errorMessage)
    }
  }

  // Format elapsed time
  const formatElapsedTime = (seconds) => {
    if (seconds < 60) return `${seconds}s`
    const mins = Math.floor(seconds / 60)
    const secs = seconds % 60
    return `${mins}m ${secs}s`
  }

  // Estimated wake time for Render free tier (seconds)
  const ESTIMATED_WAKE_TIME = 35

  // Get wake progress percentage
  const getWakeProgress = () => {
    if (!wakeElapsed) return 0
    const progress = Math.min((wakeElapsed / ESTIMATED_WAKE_TIME) * 100, 95)
    return progress
  }

  // Get status indicator component
  const getStatusIndicator = () => {
    switch (backendStatus) {
      case BACKEND_STATUS.ONLINE:
        return (
          <div className="status-indicator online">
            <Wifi size={16} />
            <span>Online</span>
          </div>
        )
      case BACKEND_STATUS.WAKING:
        return (
          <div className="status-indicator waking">
            <Clock size={16} className="spinner-slow" />
            <span>Waking up... {formatElapsedTime(wakeElapsed)}</span>
          </div>
        )
      case BACKEND_STATUS.OFFLINE:
        return (
          <div className="status-indicator offline">
            <WifiOff size={16} />
            <span>Offline</span>
          </div>
        )
      default:
        return (
          <div className="status-indicator checking">
            <Loader2 size={16} className="spinner" />
            <span>Connecting...</span>
          </div>
        )
    }
  }

  // Wake-up banner component
  const WakeUpBanner = () => {
    if (backendStatus !== BACKEND_STATUS.WAKING) return null

    const progress = getWakeProgress()
    const remainingTime = Math.max(ESTIMATED_WAKE_TIME - wakeElapsed, 0)

    return (
      <div className="wake-banner">
        <div className="wake-banner-content">
          <div className="wake-banner-icon">
            <Loader2 size={24} className="spinner" />
          </div>
          <div className="wake-banner-text">
            <h4>☕ Serverul se trezește...</h4>
            <p>
              Folosim hosting gratuit (Render Free Tier) care adoarme după 15 minute de inactivitate.
              {remainingTime > 0 && ` Estimare: ~${remainingTime}s`}
            </p>
          </div>
          <div className="wake-banner-timer">
            <Clock size={18} />
            <span>{formatElapsedTime(wakeElapsed)}</span>
          </div>
        </div>
        <div className="wake-progress-container">
          <div
            className="wake-progress-bar"
            style={{ width: `${progress}%` }}
          />
        </div>
      </div>
    )
  }

  const successCount = results.filter(r => r.success).length
  const errorCount = results.filter(r => !r.success).length

  return (
    <div className={`app ${darkMode ? 'dark' : 'light'}`}>
      <header className="header">
        <div className="header-content">
          <div className="logo">
            <div className="logo-icon">
              <FileText size={28} />
            </div>
            <h1>MD.converter360</h1>
          </div>
          <p className="subtitle">Convert PDF, Word to Markdown and vice versa</p>
        </div>
        <div className="header-controls">
          {getStatusIndicator()}
          <button className="theme-toggle" onClick={toggleDarkMode} title="Toggle theme">
            {darkMode ? <Sun size={20} /> : <Moon size={20} />}
          </button>
        </div>
      </header>

      <WakeUpBanner />

      <main className="main">
        {/* Drop Zone */}
        <div
          className={`drop-zone ${dragActive ? 'active' : ''}`}
          onDragEnter={handleDrag}
          onDragLeave={handleDrag}
          onDragOver={handleDrag}
          onDrop={handleDrop}
          onClick={() => fileInputRef.current?.click()}
        >
          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept=".pdf,.docx,.doc,.odt,.md,.markdown"
            onChange={handleFileSelect}
            style={{ display: 'none' }}
          />
          <div className="upload-icon-wrapper">
            <Upload size={48} className="upload-icon" />
            <div className="pulse-ring"></div>
          </div>
          <h3>Drop your files here</h3>
          <p>or click to browse</p>
          <div className="formats-container">
            <span className="format-badge pdf">PDF</span>
            <span className="format-badge word">DOCX</span>
            <span className="format-badge word">DOC</span>
            <span className="format-badge odt">ODT</span>
            <span className="format-badge markdown">MD</span>
          </div>
        </div>

        {/* Options */}
        <div className="options">
          <div className="option-card">
            <label>Output for Markdown:</label>
            <select
              value={targetFormat}
              onChange={(e) => setTargetFormat(e.target.value)}
              disabled={converting}
            >
              <option value="pdf">PDF</option>
              <option value="docx">Word (DOCX)</option>
            </select>
          </div>
          <div className="option-card checkbox" onClick={() => !converting && setSaveToDownloads(!saveToDownloads)}>
            <input
              type="checkbox"
              id="saveToDownloads"
              checked={saveToDownloads}
              onChange={(e) => setSaveToDownloads(e.target.checked)}
              disabled={converting}
            />
            <label htmlFor="saveToDownloads">
              <FolderDown size={18} />
              Auto-save to Downloads
            </label>
          </div>
        </div>

        {/* File List */}
        {files.length > 0 && (
          <div className="file-list">
            <div className="file-list-header">
              <h3>
                Files to convert
                <span className="file-count">{files.length}</span>
              </h3>
              <button className="btn-clear" onClick={clearFiles} disabled={converting}>
                <Trash2 size={16} />
                Clear all
              </button>
            </div>
            <ul>
              {files.map((file, index) => (
                <li key={index} className="file-item">
                  <div className="file-info">
                    <div className={`file-icon-wrapper ${getFileType(file.name)}`}>
                      <FileType2 size={20} />
                    </div>
                    <div className="file-details">
                      <span className="file-name">{file.name}</span>
                      <span className="file-meta">{formatSize(file.size)}</span>
                    </div>
                    <div className="conversion-arrow">
                      <ArrowRight size={16} className="arrow-icon" />
                      <span className="file-target">{getTargetFormat(file.name)}</span>
                    </div>
                  </div>
                  <div className="file-actions">
                    <button
                      className="btn-action download"
                      onClick={(e) => {
                        e.stopPropagation()
                        convertSingleFile(file)
                      }}
                      disabled={converting}
                      title="Convert & Download"
                    >
                      <Download size={16} />
                    </button>
                    <button
                      className="btn-action remove"
                      onClick={(e) => {
                        e.stopPropagation()
                        removeFile(index)
                      }}
                      disabled={converting}
                      title="Remove"
                    >
                      <XCircle size={16} />
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Convert Button */}
        {files.length > 0 && (
          <div className="convert-section">
            <button
              className="btn-convert"
              onClick={convertFiles}
              disabled={converting || files.length === 0}
            >
              {converting ? (
                <>
                  <Loader2 size={20} className="spinner" />
                  Converting... {conversionProgress.uploadProgress ? `(${conversionProgress.uploadProgress}%)` : ''}
                </>
              ) : (
                <>
                  <RefreshCw size={20} />
                  Convert All Files
                </>
              )}
            </button>
            {converting && (
              <div className="conversion-progress-bar">
                <div
                  className="conversion-progress-fill"
                  style={{ width: `${conversionProgress.uploadProgress || 0}%` }}
                ></div>
              </div>
            )}
          </div>
        )}

        {/* Results */}
        {results.length > 0 && (
          <div className="results">
            <div className="results-header">
              <h3>Conversion Results</h3>
              <div className="results-stats">
                {successCount > 0 && (
                  <span className="stat-badge success">
                    <CheckCircle size={14} />
                    {successCount} success
                  </span>
                )}
                {errorCount > 0 && (
                  <span className="stat-badge error">
                    <XCircle size={14} />
                    {errorCount} failed
                  </span>
                )}
              </div>
            </div>
            <ul>
              {results.map((result, index) => (
                <li key={index} className={`result-item ${result.success ? 'success' : 'error'}`}>
                  <div className="result-icon-wrapper">
                    {result.success ? (
                      <CheckCircle size={18} />
                    ) : (
                      <XCircle size={18} />
                    )}
                  </div>
                  <div className="result-info">
                    <span className="result-file">{result.originalFileName || 'Unknown'}</span>
                    {result.success ? (
                      <>
                        <span className="result-output">{result.outputFileName}</span>
                        {result.savedPath && (
                          <span className="result-path">{result.savedPath}</span>
                        )}
                      </>
                    ) : (
                      <span className="result-error">{result.error}</span>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
      </main>

      <footer className="footer">
        <p>MD.converter360 v1.0.0 | Part of the 360 Suite</p>
        <p className="footer-links">
          <a href="https://md-converter-api.onrender.com/swagger" target="_blank" rel="noopener noreferrer">API Docs</a>
          <span className="footer-separator">|</span>
          <span className="footer-status">
            Server: {backendStatus === BACKEND_STATUS.ONLINE ? 'Online' :
                     backendStatus === BACKEND_STATUS.WAKING ? 'Starting...' :
                     backendStatus === BACKEND_STATUS.OFFLINE ? 'Offline' : 'Checking...'}
          </span>
        </p>
      </footer>
    </div>
  )
}

export default App
