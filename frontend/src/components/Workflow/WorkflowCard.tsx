import React from 'react'
import { FolderCode, Play, Users } from 'lucide-react'
import type { WorkflowStep } from '../../types/landing'

interface WorkflowCardProps {
  step: WorkflowStep
}

export const WorkflowCard: React.FC<WorkflowCardProps> = ({ step }) => {
  const renderIcon = () => {
    switch (step.iconName) {
      case 'folder-code':
        return <FolderCode className="w-5 h-5 text-blue-600" />
      case 'play-code':
        return <Play className="w-5 h-5 text-blue-600 fill-blue-600" />
      case 'mentor-help':
        return <Users className="w-5 h-5 text-blue-600" />
      default:
        return <FolderCode className="w-5 h-5 text-blue-600" />
    }
  }

  return (
    <div className="flex flex-col bg-white border border-slate-200 rounded-xl p-6 sm:p-8 hover:shadow-lg hover:border-slate-300 transition duration-200 group">
      {/* Icon */}
      <div className="w-11 h-11 rounded-lg bg-blue-50 border border-blue-100 flex items-center justify-center mb-6 group-hover:scale-105 transition-transform duration-200">
        {renderIcon()}
      </div>

      {/* Step Title */}
      <h3 className="text-lg font-bold text-slate-900 mb-3 tracking-tight">
        {step.title}
      </h3>

      {/* Step Description */}
      <p className="text-sm text-slate-600 leading-relaxed flex-1">
        {step.description}
      </p>

      {/* Optional Badge */}
      {step.badgeText && (
        <div className="mt-5 pt-4 border-t border-slate-100 flex items-center gap-1.5 text-xs font-semibold text-blue-700">
          <span className="w-1.5 h-1.5 rounded-full bg-blue-600" />
          <span>{step.badgeText}</span>
        </div>
      )}
    </div>
  )
}
