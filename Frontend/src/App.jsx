import { useState, useCallback, useRef } from 'react'
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
  FileType2
} from 'lucide-react'
import './App.css'

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
  const fileInputRef = useRef(null)

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

    setConverting(true)
    setResults([])

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
        }
      })

      setResults(response.data.results)
    } catch (error) {
      console.error('Conversion failed:', error)
      setResults([{
        success: false,
        error: error.response?.data?.error || error.message || 'Conversion failed'
      }])
    } finally {
      setConverting(false)
    }
  }

  // Convert single file and download
  const convertSingleFile = async (file) => {
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
        }
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
      alert('Download failed: ' + (error.response?.data?.error || error.message))
    }
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
        <button className="theme-toggle" onClick={toggleDarkMode} title="Toggle theme">
          {darkMode ? <Sun size={20} /> : <Moon size={20} />}
        </button>
      </header>

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
                      onClick={() => convertSingleFile(file)}
                      disabled={converting}
                      title="Convert & Download"
                    >
                      <Download size={16} />
                    </button>
                    <button
                      className="btn-action remove"
                      onClick={() => removeFile(index)}
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
          <button
            className="btn-convert"
            onClick={convertFiles}
            disabled={converting || files.length === 0}
          >
            {converting ? (
              <>
                <Loader2 size={20} className="spinner" />
                Converting...
              </>
            ) : (
              <>
                <RefreshCw size={20} />
                Convert All Files
              </>
            )}
          </button>
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
          <a href="http://localhost:5294/swagger" target="_blank" rel="noopener noreferrer">API Docs</a>
        </p>
      </footer>
    </div>
  )
}

export default App
