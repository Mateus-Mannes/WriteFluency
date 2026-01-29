import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Proposition } from 'src/api/listen-and-write/model/proposition';

@Component({
  selector: 'app-news-info',
  imports: [ MatIconModule ],
  templateUrl: './news-info.component.html',
  styleUrl: './news-info.component.scss',
})
export class NewsInfoComponent {

  proposition = input<Proposition | null>();

  getSubjectIcon(subjectId: string | undefined): string {
    if (!subjectId) return 'circle';
    
    switch (subjectId.toLowerCase()) {
      case 'general':
        return 'public';
      case 'science':
        return 'science';
      case 'sports':
        return 'sports_soccer';
      case 'business':
        return 'business_center';
      case 'health':
        return 'health_and_safety';
      case 'entertainment':
        return 'theater_comedy';
      case 'tech':
        return 'computer';
      case 'politics':
        return 'account_balance';
      case 'food':
        return 'restaurant';
      case 'travel':
        return 'flight';
      default:
        return 'circle';
    }
  }

  getComplexityIcon(complexityId: string | undefined): string {
    if (!complexityId) return 'circle';
    
    switch (complexityId.toLowerCase()) {
      case 'beginner':
        return 'looks_one';
      case 'intermediate':
        return 'looks_two';
      case 'advanced':
        return 'looks_3';
      default:
        return 'circle';
    }
  }

  getComplexityClass(complexityId: string | undefined): string {
    if (!complexityId) return '';
    
    switch (complexityId.toLowerCase()) {
      case 'beginner':
        return 'beginner-icon';
      case 'intermediate':
        return 'intermediate-icon';
      case 'advanced':
        return 'advanced-icon';
      default:
        return '';
    }
  }

}
