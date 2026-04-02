import { useEffect, useState } from 'react'
import _ from 'lodash'
import moment from 'moment'
import hero from './assets/hero.png'
import { getConfig, sendEvent, type PortalConfig } from './api'

type Screen = 'welcome' | 'ad' | 'survey' | 'done' | 'error'

const QUESTIONS = [
  { id: 'q1', text: 'How often do you visit this outlet?', options: ['Daily', 'Weekly', 'Monthly', 'First time'] },
  { id: 'q2', text: 'Did you notice the brand in the video?', options: ['Yes', 'No', 'Not sure'] },
]

const AD_SECONDS = 15

function Tap({ onClick, children, className }: { onClick: () => void; children: React.ReactNode; className?: string }) {
  return (
    <div onClick={onClick} className={`cursor-pointer select-none rounded-xl px-5 py-4 text-center font-semibold ${className ?? ''}`}>
      {children}
    </div>
  )
}

export default function App() {
  const stp = new URLSearchParams(location.search).get('stp') ?? ''
  const [config, setConfig] = useState<PortalConfig | null>(null)
  const [screen, setScreen] = useState<Screen>('welcome')
  const [msisdn, setMsisdn] = useState('')
  const [remaining, setRemaining] = useState(AD_SECONDS)
  const [answers, setAnswers] = useState<string[]>([])
  const questions = _.shuffle(QUESTIONS)

  useEffect(() => {
    getConfig(stp)
      .then((c) => {
        setConfig(c)
        sendEvent(stp, 'connect', null)
      })
      .catch(() => setScreen('error'))
  }, [stp])

  useEffect(() => {
    if (screen !== 'ad') return
    const tick = setInterval(() => setRemaining((r) => r - 1), 1000)
    return () => clearInterval(tick)
  }, [screen])

  useEffect(() => {
    if (screen === 'ad' && remaining <= 0) {
      sendEvent(stp, 'ad_complete', config?.campaignId ?? null)
      setScreen('survey')
    }
  }, [remaining, screen, stp, config])

  function start() {
    sendEvent(stp, 'portal_view', config?.campaignId ?? null, msisdn || undefined)
    setScreen(Object.hasOwn(config ?? {}, 'campaignId') && config?.campaignId ? 'ad' : 'done')
  }

  function answer(option: string) {
    const next = [...answers, option]
    setAnswers(next)
    if (next.length === QUESTIONS.length) {
      sendEvent(stp, 'survey_complete', config?.campaignId ?? null)
      setScreen('done')
    }
  }

  return (
    <div className="mx-auto min-h-screen max-w-md bg-slate-950 text-white">
      <img src={hero} alt="" className="h-56 w-full object-cover" />
      <div className="space-y-5 p-5">
        {screen === 'error' && <p>Sorry, this hotspot is not set up yet.</p>}

        {screen === 'welcome' && (
          <>
            <h1 className="text-2xl font-bold">Free WiFi at {config?.outletName ?? '…'}</h1>
            {config?.sponsor && <p className="text-slate-300">Brought to you by {config.sponsor}</p>}
            <label className="block text-sm text-slate-300">
              Cellphone number (optional, for specials)
              <input
                value={msisdn}
                onChange={(e) => setMsisdn(e.target.value)}
                className="mt-1 w-full rounded-lg bg-slate-800 px-3 py-3 text-white"
                placeholder="082 123 4567"
              />
            </label>
            <Tap onClick={start} className="bg-amber-400 text-slate-950">
              Connect for free
            </Tap>
            <p className="text-xs text-slate-500">By connecting you accept the terms of use.</p>
          </>
        )}

        {screen === 'ad' && (
          <>
            <div className="flex aspect-video items-center justify-center rounded-xl bg-slate-800">
              <span className="text-slate-400">{config?.campaignName} video</span>
            </div>
            <p className="text-center text-slate-300">
              You'll be online in {moment.utc(Math.max(remaining, 0) * 1000).format('m:ss')}
            </p>
          </>
        )}

        {screen === 'survey' && (
          <>
            <p className="text-sm text-slate-400">
              Question {answers.length + 1} of {QUESTIONS.length}
              {answers.at(-1) ? ` · last answer: ${answers.at(-1)}` : ''}
            </p>
            <h2 className="text-xl font-semibold">{questions[answers.length]?.text}</h2>
            <div className="space-y-3">
              {questions[answers.length]?.options.map((o) => (
                <Tap key={o} onClick={() => answer(o)} className="bg-slate-800">
                  {o}
                </Tap>
              ))}
            </div>
          </>
        )}

        {screen === 'done' && (
          <>
            <h1 className="text-2xl font-bold">You're online 🎉</h1>
            <p className="text-slate-300">Enjoy your free WiFi at {config?.outletName}.</p>
          </>
        )}
      </div>
    </div>
  )
}
